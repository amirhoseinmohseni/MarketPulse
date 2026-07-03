namespace MarketPulse.Application.Services.RedditDataCollection
{
    public class RedditPostSearchRequest
    {
        public string Query { get; init; } = string.Empty;

        public string? Subreddit { get; init; }

        public int Limit { get; init; } = 25;

        public string Sort { get; init; } = "relevance";

        public string TimeRange { get; init; } = "year";
    }
}
