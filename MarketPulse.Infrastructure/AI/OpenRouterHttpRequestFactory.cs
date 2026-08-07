using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MarketPulse.Infrastructure.AI;

internal static class OpenRouterHttpRequestFactory
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static HttpRequestMessage Create(OpenRouterOptions options, object payload)
    {
        EnsureConfigured(options);

        var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        return request;
    }

    private static void EnsureConfigured(OpenRouterOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "OpenRouter API key is not configured. Set OpenRouter:ApiKey.");
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                "OpenRouter endpoint must be a valid absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new InvalidOperationException(
                "OpenRouter model is not configured. Set OpenRouter:Model.");
        }
    }
}
