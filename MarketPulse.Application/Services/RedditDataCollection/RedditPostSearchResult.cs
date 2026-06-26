namespace MarketPulse.Application.Services.RedditDataCollection
{
    public class RedditPostSearchResult
    {
        public string RedditPostId { get; init; } = string.Empty;

        public string Subreddit { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string? SelfText { get; init; }

        public string? Url { get; init; }

        public string Permalink { get; init; } = string.Empty;

        public int Score { get; init; }

        public int CommentCount { get; init; }

        public DateTime CreatedUtc { get; init; }
    }
}
