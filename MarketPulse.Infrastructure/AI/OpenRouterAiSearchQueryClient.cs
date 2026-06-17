using MarketPulse.Application.Services.SearchQueryGenerator;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MarketPulse.Infrastructure.AI
{
    public sealed class OpenRouterAiSearchQueryClient : IAiSearchQueryClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new InvalidOperationException("OpenRouter API key is not configured. Set OpenRouter:ApiKey.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var payload = new
            {
                model = _options.Model,
                messages = new[]
                {
                    new { role = "system", content = prompt.SystemPrompt },
                    new { role = "user", content = prompt.UserPrompt }
                },
                temperature = _options.Temperature,
                max_tokens = _options.MaxTokens
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateOpenRouterException(response.StatusCode, responseBody);
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

        private static HttpRequestException CreateOpenRouterException(
            HttpStatusCode statusCode,
            string responseBody)
        {
            return new HttpRequestException(
                $"OpenRouter request failed with status code {(int)statusCode} ({statusCode}). Response body: {responseBody}",
                null,
                statusCode);
        }
    }
}
