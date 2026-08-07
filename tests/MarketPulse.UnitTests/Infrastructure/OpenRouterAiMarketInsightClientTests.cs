using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MarketPulse.Application.Services.Analyser;
using MarketPulse.Domain.Enums;
using MarketPulse.Infrastructure;
using MarketPulse.Infrastructure.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketPulse.UnitTests.Infrastructure;

public class OpenRouterAiMarketInsightClientTests
{
    [Fact]
    public void InfrastructureRegistration_BindsOpenRouterTransportSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenRouter:ApiKey"] = "configuration-test-key",
                ["OpenRouter:Model"] = "test/configured-model",
                ["OpenRouter:Endpoint"] = "https://configured.openrouter.test/chat",
                ["OpenRouter:Temperature"] = "0.35",
                ["OpenRouter:MaxTokens"] = "1400",
                ["OpenRouter:TimeoutSeconds"] = "42",
                ["OpenRouter:MaxRetryAttempts"] = "3",
                ["OpenRouter:RetryBaseDelayMilliseconds"] = "125"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<OpenRouterOptions>();

        Assert.Equal("test/configured-model", options.Model);
        Assert.Equal("https://configured.openrouter.test/chat", options.Endpoint);
        Assert.Equal(0.35, options.Temperature);
        Assert.Equal(1_400, options.MaxTokens);
        Assert.Equal(42, options.TimeoutSeconds);
        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.Equal(125, options.RetryBaseDelayMilliseconds);
        Assert.IsType<OpenRouterAiMarketInsightClient>(
            provider.GetRequiredService<IAiMarketInsightClient>());
    }

    [Fact]
    public async Task RequestPayload_UsesStrictApplicationSchemaAndOnlyTwoPromptMessages()
    {
        string? requestBody = null;
        AuthenticationHeaderValue? authorization = null;
        Uri? requestUri = null;
        var handler = new StubHttpMessageHandler(async (request, _, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            authorization = request.Headers.Authorization;
            requestUri = request.RequestUri;
            return JsonResponse(
                HttpStatusCode.OK,
                SuccessfulEnvelope("{\"summary\":\"valid\"}"));
        });
        var options = CreateOptions();
        var client = CreateClient(handler, options);
        var applicationRequest = CreateApplicationRequest();

        await client.GenerateAsync(applicationRequest);

        Assert.NotNull(requestBody);
        using var document = JsonDocument.Parse(requestBody);
        var root = document.RootElement;

        Assert.Equal(options.Endpoint, requestUri?.ToString());
        Assert.Equal("Bearer", authorization?.Scheme);
        Assert.Equal(options.ApiKey, authorization?.Parameter);
        Assert.Equal(options.Model, root.GetProperty("model").GetString());
        Assert.Equal(options.Temperature, root.GetProperty("temperature").GetDouble());
        Assert.Equal(options.MaxTokens, root.GetProperty("max_tokens").GetInt32());
        Assert.False(root.GetProperty("stream").GetBoolean());

        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal(applicationRequest.SystemPrompt, messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal(applicationRequest.UserPrompt, messages[1].GetProperty("content").GetString());

        Assert.True(
            root.GetProperty("provider")
                .GetProperty("require_parameters")
                .GetBoolean());

        var responseFormat = root.GetProperty("response_format");
        Assert.Equal("json_schema", responseFormat.GetProperty("type").GetString());
        var jsonSchema = responseFormat.GetProperty("json_schema");
        Assert.Equal(applicationRequest.ResponseSchemaName, jsonSchema.GetProperty("name").GetString());
        Assert.True(jsonSchema.GetProperty("strict").GetBoolean());
        Assert.False(
            jsonSchema.GetProperty("schema")
                .GetProperty("additionalProperties")
                .GetBoolean());

        var expectedSchema = JsonNode.Parse(applicationRequest.ResponseJsonSchema);
        var sentSchema = JsonNode.Parse(jsonSchema.GetProperty("schema").GetRawText());
        Assert.True(JsonNode.DeepEquals(expectedSchema, sentSchema));

        Assert.False(root.TryGetProperty("tools", out _));
        Assert.False(root.TryGetProperty("plugins", out _));
        Assert.False(root.TryGetProperty("web_search", out _));
        Assert.DoesNotContain(options.ApiKey, requestBody);
    }

    [Fact]
    public async Task ValidResponse_ReturnsStructuredMessageContent()
    {
        const string content = """
            {"marketScore":null,"signalStrength":"Weak","summary":"Grounded","strengths":[],"weaknesses":[],"opportunities":[],"risks":[]}
            """;
        var handler = SingleResponseHandler(
            JsonResponse(HttpStatusCode.OK, SuccessfulEnvelope(content)));
        var client = CreateClient(handler);

        var result = await client.GenerateAsync(CreateApplicationRequest());

        Assert.Equal(content, result);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[null]}")]
    public async Task MalformedResponseEnvelope_IsRejected(string responseBody)
    {
        var client = CreateClient(
            SingleResponseHandler(JsonResponse(HttpStatusCode.OK, responseBody)));

        await Assert.ThrowsAsync<OpenRouterProtocolException>(
            () => client.GenerateAsync(CreateApplicationRequest()));
    }

    [Fact]
    public async Task MalformedStructuredMessageContent_IsRejected()
    {
        var client = CreateClient(
            SingleResponseHandler(
                JsonResponse(HttpStatusCode.OK, SuccessfulEnvelope("{not-json"))));

        await Assert.ThrowsAsync<OpenRouterProtocolException>(
            () => client.GenerateAsync(CreateApplicationRequest()));
    }

    [Fact]
    public async Task EmptyMessageContent_IsRejected()
    {
        var client = CreateClient(
            SingleResponseHandler(
                JsonResponse(HttpStatusCode.OK, SuccessfulEnvelope("  "))));

        await Assert.ThrowsAsync<OpenRouterProtocolException>(
            () => client.GenerateAsync(CreateApplicationRequest()));
    }

    [Fact]
    public async Task TruncatedCompletion_IsRejected()
    {
        var client = CreateClient(
            SingleResponseHandler(
                JsonResponse(
                    HttpStatusCode.OK,
                    SuccessfulEnvelope("{\"partial\":true}", "length"))));

        var exception = await Assert.ThrowsAsync<OpenRouterProtocolException>(
            () => client.GenerateAsync(CreateApplicationRequest()));

        Assert.Contains("length", exception.Message);
    }

    [Fact]
    public async Task UntrustedFinishReason_IsNotCopiedIntoException()
    {
        const string sensitiveFinishReason = "SENSITIVE PROVIDER RESPONSE CONTENT";
        var client = CreateClient(
            SingleResponseHandler(
                JsonResponse(
                    HttpStatusCode.OK,
                    SuccessfulEnvelope("{\"partial\":true}", sensitiveFinishReason))));

        var exception = await Assert.ThrowsAsync<OpenRouterProtocolException>(
            () => client.GenerateAsync(CreateApplicationRequest()));

        Assert.DoesNotContain(sensitiveFinishReason, exception.Message);
        Assert.Contains("unknown", exception.Message);
    }

    [Fact]
    public async Task Http200ErrorEnvelope_IsRejected()
    {
        var client = CreateClient(
            SingleResponseHandler(
                JsonResponse(
                    HttpStatusCode.OK,
                    "{\"error\":{\"code\":503,\"message\":\"provider failed\"}}")));

        await Assert.ThrowsAsync<OpenRouterProtocolException>(
            () => client.GenerateAsync(CreateApplicationRequest()));
    }

    [Fact]
    public async Task Unauthorized_IsNotRetriedAndDoesNotExposeResponseBody()
    {
        var handler = SingleResponseHandler(
            JsonResponse(
                HttpStatusCode.Unauthorized,
                "{\"error\":{\"code\":401,\"message\":\"SENSITIVE-PROVIDER-BODY\"}}"));
        var delay = new RecordingRetryDelay();
        var client = CreateClient(handler, retryDelay: delay);

        var exception = await Assert.ThrowsAsync<OpenRouterRequestException>(
            () => client.GenerateAsync(CreateApplicationRequest()));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal("401", exception.ProviderErrorCode);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(delay.Delays);
        Assert.DoesNotContain("SENSITIVE-PROVIDER-BODY", exception.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task TransientStatus_RetriesAndRespectsRetryAfter(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler((_, attempt, _) =>
        {
            if (attempt == 1)
            {
                var response = JsonResponse(
                    statusCode,
                    $"{{\"error\":{{\"code\":{(int)statusCode},\"message\":\"retry\"}}}}");
                response.Headers.RetryAfter = new RetryConditionHeaderValue(
                    TimeSpan.FromSeconds(3));
                return Task.FromResult(response);
            }

            return Task.FromResult(
                JsonResponse(
                    HttpStatusCode.OK,
                    SuccessfulEnvelope("{\"ok\":true}")));
        });
        var delay = new RecordingRetryDelay();
        var client = CreateClient(handler, retryDelay: delay);

        var result = await client.GenerateAsync(CreateApplicationRequest());

        Assert.Equal("{\"ok\":true}", result);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal(TimeSpan.FromSeconds(3), Assert.Single(delay.Delays));
    }

    [Fact]
    public async Task RetryCount_IsBounded()
    {
        var options = CreateOptions(maxRetryAttempts: 2);
        var handler = new StubHttpMessageHandler((_, _, _) =>
            Task.FromResult(
                JsonResponse(
                    HttpStatusCode.ServiceUnavailable,
                    "{\"error\":{\"code\":503,\"message\":\"still unavailable\"}}")));
        var delay = new RecordingRetryDelay();
        var client = CreateClient(handler, options, delay);

        var exception = await Assert.ThrowsAsync<OpenRouterRequestException>(
            () => client.GenerateAsync(CreateApplicationRequest()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal(3, handler.CallCount);
        Assert.Equal(2, delay.Delays.Count);
    }

    [Fact]
    public async Task Timeout_IsReportedWithoutRetry()
    {
        var handler = new StubHttpMessageHandler(async (_, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        });
        var options = CreateOptions(timeoutSeconds: 1);
        var delay = new RecordingRetryDelay();
        var client = CreateClient(handler, options, delay);

        await Assert.ThrowsAsync<TimeoutException>(
            () => client.GenerateAsync(CreateApplicationRequest()));

        Assert.Equal(1, handler.CallCount);
        Assert.Empty(delay.Delays);
    }

    [Fact]
    public async Task CallerCancellation_StopsRetryDelay()
    {
        var handler = new StubHttpMessageHandler((_, _, _) =>
            Task.FromResult(
                JsonResponse(
                    HttpStatusCode.TooManyRequests,
                    "{\"error\":{\"code\":429,\"message\":\"retry later\"}}")));
        var delay = new BlockingRetryDelay();
        var client = CreateClient(handler, retryDelay: delay);
        using var cancellationSource = new CancellationTokenSource();

        var generation = client.GenerateAsync(
            CreateApplicationRequest(),
            cancellationSource.Token);
        await delay.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => generation);
        Assert.Equal(1, handler.CallCount);
    }

    private static AiMarketInsightRequest CreateApplicationRequest()
    {
        var builder = new MarketInsightPromptBuilder(new MarketInsightAnalysisOptions());
        return builder.Build(
            new AiMarketInsightInput(
                "Allowed idea",
                [
                    new AiMarketInsightInputItem(
                        "C001",
                        "HackerNews",
                        "Allowed title",
                        "Allowed collected content",
                        10,
                        2,
                        new DateTime(2026, 7, 24, 0, 0, 0, DateTimeKind.Utc))
                ]),
            SignalStrength.Weak);
    }

    private static OpenRouterAiMarketInsightClient CreateClient(
        StubHttpMessageHandler handler,
        OpenRouterOptions? options = null,
        IOpenRouterRetryDelay? retryDelay = null)
    {
        var httpClient = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        return new OpenRouterAiMarketInsightClient(
            httpClient,
            options ?? CreateOptions(),
            retryDelay ?? new RecordingRetryDelay(),
            NullLogger<OpenRouterAiMarketInsightClient>.Instance);
    }

    private static OpenRouterOptions CreateOptions(
        int timeoutSeconds = 5,
        int maxRetryAttempts = 2)
        => new()
        {
            ApiKey = "test-openrouter-key",
            Model = "test/structured-model",
            Endpoint = "https://openrouter.test/api/v1/chat/completions",
            Temperature = 0.15,
            MaxTokens = 1_200,
            TimeoutSeconds = timeoutSeconds,
            MaxRetryAttempts = maxRetryAttempts,
            RetryBaseDelayMilliseconds = 10
        };

    private static StubHttpMessageHandler SingleResponseHandler(
        HttpResponseMessage response)
        => new((_, _, _) => Task.FromResult(response));

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private static string SuccessfulEnvelope(
        string content,
        string finishReason = "stop")
        => JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content
                    },
                    finish_reason = finishReason
                }
            }
        });

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        private int _callCount;

        public int CallCount => _callCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _callCount);
            return handler(request, attempt, cancellationToken);
        }
    }

    private sealed class RecordingRetryDelay : IOpenRouterRetryDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingRetryDelay : IOpenRouterRetryDelay
    {
        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task DelayAsync(
            TimeSpan delay,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
