namespace MarketPulse.Infrastructure.AI
{
    public sealed class OpenRouterOptions
    {
        public const string SectionName = "OpenRouter";

        public string ApiKey { get; init; } = string.Empty;
        public string Model { get; init; } = "openrouter/auto";
        public string Endpoint { get; init; } = "https://openrouter.ai/api/v1/chat/completions";
        public double Temperature { get; init; } = 0.2;
        public int MaxTokens { get; init; } = 800;
        public int TimeoutSeconds { get; init; } = 60;
        public int MaxRetryAttempts { get; init; } = 2;
        public int RetryBaseDelayMilliseconds { get; init; } = 500;
    }
}
