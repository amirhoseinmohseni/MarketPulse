namespace MarketPulse.Infrastructure.Reddit
{
    public sealed class RedditOptions
    {
        public const string SectionName = "Reddit";

        public string ClientId { get; init; } = string.Empty;

        public string ClientSecret { get; init; } = string.Empty;

        public string UserAgent { get; init; } = "MarketPulse/1.0";

        public string AuthUrl { get; init; } = "https://www.reddit.com/api/v1/access_token";

        public string BaseUrl { get; init; } = "https://oauth.reddit.com";

        public int DefaultLimit { get; init; } = 25;
    }
}
