using MarketPulse.Application.Services.SearchQueryGenerator;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MarketPulse.Infrastructure.AI
{
    public sealed class OpenAiSearchQueryClient : IAiSearchQueryClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly OpenAiOptions _options;

        public OpenAiSearchQueryClient(HttpClient httpClient, OpenAiOptions options)
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
                throw new InvalidOperationException("OpenAI API key is not configured. Set OpenAi:ApiKey.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var payload = new
            {
                model = _options.Model,
                temperature = _options.Temperature,
                max_tokens = _options.MaxTokens,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "system", content = prompt.SystemPrompt },
                    new { role = "user", content = prompt.UserPrompt }
                }
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(responseBody);

            return document
                .RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()!;
        }
    }
}
