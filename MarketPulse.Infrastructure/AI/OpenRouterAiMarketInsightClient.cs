using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MarketPulse.Application.Services.Analyser;
using Microsoft.Extensions.Logging;

namespace MarketPulse.Infrastructure.AI;

public sealed class OpenRouterAiMarketInsightClient : IAiMarketInsightClient
{
    private static readonly Regex SafeProtocolTokenPattern = new(
        "^[A-Za-z0-9_.-]{1,64}$",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<HttpStatusCode> TransientStatusCodes =
    [
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.ServiceUnavailable
    ];

    private readonly HttpClient _httpClient;
    private readonly OpenRouterOptions _options;
    private readonly IOpenRouterRetryDelay _retryDelay;
    private readonly ILogger<OpenRouterAiMarketInsightClient> _logger;

    public OpenRouterAiMarketInsightClient(
        HttpClient httpClient,
        OpenRouterOptions options,
        IOpenRouterRetryDelay retryDelay,
        ILogger<OpenRouterAiMarketInsightClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _retryDelay = retryDelay;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(
        AiMarketInsightRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var payload = BuildPayload(request);
        var maxRetryAttempts = Math.Clamp(_options.MaxRetryAttempts, 0, 5);
        if (_options.TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                "OpenRouter timeout must be greater than zero seconds.");
        }

        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            HttpResponseMessage response;
            try
            {
                using var httpRequest = OpenRouterHttpRequestFactory.Create(_options, payload);
                response = await _httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutSource.Token);
            }
            catch (OperationCanceledException exception)
                when (!cancellationToken.IsCancellationRequested
                    && timeoutSource.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"OpenRouter request timed out after {_options.TimeoutSeconds} seconds.",
                    exception);
            }

            using (response)
            {
                string responseBody;
                try
                {
                    responseBody = await response.Content.ReadAsStringAsync(timeoutSource.Token);
                }
                catch (OperationCanceledException exception)
                    when (!cancellationToken.IsCancellationRequested
                        && timeoutSource.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"OpenRouter response timed out after {_options.TimeoutSeconds} seconds.",
                        exception);
                }

                if (!response.IsSuccessStatusCode)
                {
                    if (TransientStatusCodes.Contains(response.StatusCode)
                        && attempt < maxRetryAttempts)
                    {
                        var delay = GetRetryDelay(response, attempt);
                        _logger.LogWarning(
                            "OpenRouter Market Insight request returned transient status {StatusCode}. Retrying attempt {RetryAttempt} of {MaxRetryAttempts} after {DelayMilliseconds} ms.",
                            (int)response.StatusCode,
                            attempt + 1,
                            maxRetryAttempts,
                            delay.TotalMilliseconds);

                        await _retryDelay.DelayAsync(delay, cancellationToken);
                        continue;
                    }

                    throw CreateRequestException(response.StatusCode, responseBody);
                }

                return ReadCompletedContent(responseBody);
            }
        }
    }

    private object BuildPayload(AiMarketInsightRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SystemPrompt)
            || string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            throw new ArgumentException(
                "OpenRouter Market Insight prompts must not be empty.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ResponseSchemaName))
        {
            throw new ArgumentException(
                "The response schema name must not be empty.",
                nameof(request));
        }

        JsonElement schema;
        try
        {
            using var schemaDocument = JsonDocument.Parse(request.ResponseJsonSchema);
            schema = schemaDocument.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "The Application response schema is not valid JSON.",
                nameof(request),
                exception);
        }

        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "The Application response schema must be a JSON object.",
                nameof(request));
        }

        if (!schema.TryGetProperty("additionalProperties", out var additionalProperties)
            || additionalProperties.ValueKind != JsonValueKind.False)
        {
            throw new ArgumentException(
                "The Application response schema must set additionalProperties to false.",
                nameof(request));
        }

        return new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens,
            stream = false,
            provider = new
            {
                require_parameters = true
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = request.ResponseSchemaName,
                    strict = true,
                    schema
                }
            }
        };
    }

    private static string ReadCompletedContent(string responseBody)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(responseBody);
        }
        catch (JsonException exception)
        {
            throw new OpenRouterProtocolException(
                "OpenRouter returned a malformed JSON response envelope.",
                exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (TryGetErrorCode(root, out var topLevelErrorCode))
            {
                throw new OpenRouterProtocolException(
                    $"OpenRouter returned an error payload with HTTP 200 and provider error code '{topLevelErrorCode}'.");
            }

            if (!root.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                throw new OpenRouterProtocolException(
                    "OpenRouter response did not contain a completion choice.");
            }

            var choice = choices[0];
            if (choice.ValueKind != JsonValueKind.Object)
            {
                throw new OpenRouterProtocolException(
                    "OpenRouter completion choice was malformed.");
            }

            if (TryGetErrorCode(choice, out var choiceErrorCode))
            {
                throw new OpenRouterProtocolException(
                    $"OpenRouter completion failed with provider error code '{choiceErrorCode}'.");
            }

            if (!choice.TryGetProperty("finish_reason", out var finishReasonElement)
                || finishReasonElement.ValueKind != JsonValueKind.String)
            {
                throw new OpenRouterProtocolException(
                    "OpenRouter response did not contain a valid finish reason.");
            }

            var finishReason = finishReasonElement.GetString();
            if (!string.Equals(finishReason, "stop", StringComparison.Ordinal))
            {
                throw new OpenRouterProtocolException(
                    $"OpenRouter completion was not complete. Finish reason: '{SanitizeProtocolToken(finishReason)}'.");
            }

            if (!choice.TryGetProperty("message", out var message)
                || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("content", out var contentElement)
                || contentElement.ValueKind != JsonValueKind.String)
            {
                throw new OpenRouterProtocolException(
                    "OpenRouter response did not contain string message content.");
            }

            if (message.TryGetProperty("refusal", out var refusal)
                && refusal.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(refusal.GetString()))
            {
                throw new OpenRouterProtocolException(
                    "OpenRouter model refused to produce the requested structured output.");
            }

            var content = contentElement.GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new OpenRouterProtocolException(
                    "OpenRouter returned empty message content.");
            }

            try
            {
                using var _ = JsonDocument.Parse(content);
            }
            catch (JsonException exception)
            {
                throw new OpenRouterProtocolException(
                    "OpenRouter message content was not valid JSON.",
                    exception);
            }

            return content;
        }
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta >= TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter?.Date is { } retryDate)
        {
            var dateDelay = retryDate - DateTimeOffset.UtcNow;
            if (dateDelay > TimeSpan.Zero)
            {
                return dateDelay;
            }
        }

        var multiplier = Math.Pow(2, attempt);
        return TimeSpan.FromMilliseconds(
            Math.Max(0, _options.RetryBaseDelayMilliseconds) * multiplier);
    }

    private static OpenRouterRequestException CreateRequestException(
        HttpStatusCode statusCode,
        string responseBody)
    {
        string? providerErrorCode = null;
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            TryGetErrorCode(document.RootElement, out providerErrorCode);
        }
        catch (JsonException)
        {
            // The response body is intentionally not included in logs or exceptions.
        }

        return new OpenRouterRequestException(statusCode, providerErrorCode);
    }

    private static bool TryGetErrorCode(
        JsonElement element,
        out string? errorCode)
    {
        errorCode = null;
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("error", out var error)
            || error.ValueKind != JsonValueKind.Object
            || !error.TryGetProperty("code", out var code))
        {
            return false;
        }

        var candidate = code.ValueKind switch
        {
            JsonValueKind.String => code.GetString(),
            JsonValueKind.Number => code.GetRawText(),
            _ => "unknown"
        };

        errorCode = SanitizeProtocolToken(candidate);

        return true;
    }

    private static string SanitizeProtocolToken(string? value)
        => value is not null && SafeProtocolTokenPattern.IsMatch(value)
            ? value
            : "unknown";
}
