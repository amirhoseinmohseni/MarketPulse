namespace MarketPulse.Infrastructure.HackerNews
{
    public sealed class HackerNewsOptions
    {
        public const string SectionName = "HackerNews";

        public string BaseUrl { get; init; } = "https://hn.algolia.com/api/v1";

        public int DefaultLimit { get; init; } = 20;

        public string Tags { get; init; } = "story";
    }
}
