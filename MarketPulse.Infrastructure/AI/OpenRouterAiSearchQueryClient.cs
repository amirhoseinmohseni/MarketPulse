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
