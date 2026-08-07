using System.Net;

namespace MarketPulse.Infrastructure.AI;

public sealed class OpenRouterProtocolException : Exception
{
    public OpenRouterProtocolException(string message)
        : base(message)
    {
    }

    public OpenRouterProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class OpenRouterRequestException : HttpRequestException
{
    public OpenRouterRequestException(
        HttpStatusCode statusCode,
        string? providerErrorCode = null)
        : base(
            CreateMessage(statusCode, providerErrorCode),
            null,
            statusCode)
    {
        ProviderErrorCode = providerErrorCode;
    }

    public string? ProviderErrorCode { get; }

    private static string CreateMessage(
        HttpStatusCode statusCode,
        string? providerErrorCode)
        => string.IsNullOrWhiteSpace(providerErrorCode)
            ? $"OpenRouter request failed with status code {(int)statusCode} ({statusCode})."
            : $"OpenRouter request failed with status code {(int)statusCode} ({statusCode}) and provider error code '{providerErrorCode}'.";
}
