namespace MarketPulse.Infrastructure.AI
{
    public sealed class OpenAiOptions
    {
        public const string SectionName = "OpenAi";

        public string ApiKey { get; init; } = string.Empty;
        public string Model { get; init; } = "gpt-4o-mini";
        public string Endpoint { get; init; } = "https://api.openai.com/v1/chat/completions";
        public double Temperature { get; init; } = 0.2;
        public int MaxTokens { get; init; } = 800;
    }
}
