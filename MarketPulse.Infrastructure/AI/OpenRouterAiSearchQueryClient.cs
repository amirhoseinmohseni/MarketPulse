using MarketPulse.Application.Services.SearchQueryGenerator;
using System.Text.Json;

namespace MarketPulse.Infrastructure.AI
{
    public sealed class OpenRouterAiSearchQueryClient : IAiSearchQueryClient
    {
        private readonly HttpClient _httpClient;
        private readonly OpenRouterOptions _options;

        public OpenRouterAiSearchQueryClient(HttpClient httpClient, OpenRouterOptions options)
        {
            _httpClient = httpClient;
            _options = options;
        }

        public async Task<string> GenerateAsync(
            AiSearchQueryPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(prompt);

            if (string.IsNullOrWhiteSpace(prompt.SystemPrompt)
                || string.IsNullOrWhiteSpace(prompt.UserPrompt)
                || string.IsNullOrWhiteSpace(prompt.ResponseSchemaName))
            {
                throw new ArgumentException(
                    "OpenRouter search-query prompts and schema name must not be empty.",
                    nameof(prompt));
            }

            JsonElement schema;
            try
            {
                using var schemaDocument = JsonDocument.Parse(prompt.ResponseJsonSchema);
                schema = schemaDocument.RootElement.Clone();
            }
            catch (JsonException exception)
            {
                throw new ArgumentException(
                    "The Application search-query response schema is not valid JSON.",
                    nameof(prompt),
                    exception);
            }

            if (schema.ValueKind != JsonValueKind.Object
                || !schema.TryGetProperty("additionalProperties", out var additionalProperties)
                || additionalProperties.ValueKind != JsonValueKind.False)
            {
                throw new ArgumentException(
                    "The Application search-query response schema must be a strict JSON object.",
                    nameof(prompt));
            }

            var payload = new
            {
                model = _options.Model,
                messages = new[]
                {
                    new { role = "system", content = prompt.SystemPrompt },
                    new { role = "user", content = prompt.UserPrompt }
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
                        name = prompt.ResponseSchemaName,
                        strict = true,
                        schema
                    }
                }
            };

            using var request = OpenRouterHttpRequestFactory.Create(_options, payload);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new OpenRouterRequestException(response.StatusCode);
            }

            using var document = JsonDocument.Parse(responseBody);

            var content = document
                .RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return content ?? string.Empty;
        }
    }
}
