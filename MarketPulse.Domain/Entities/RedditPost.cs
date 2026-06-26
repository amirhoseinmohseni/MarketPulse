namespace MarketPulse.Domain.Entities
{
    public class RedditPost
    {
        public Guid Id { get; set; }

        public Guid AnalysisRequestId { get; set; }
        public AnalysisRequest? Request { get; set; }

        public Guid? SearchQueryId { get; set; }
        public SearchQuery? SearchQuery { get; set; }

        public string RedditPostId { get; set; } = string.Empty;

        public string Subreddit { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? SelfText { get; set; }

        public string? Url { get; set; }

        public string Permalink { get; set; } = string.Empty;

        public int Score { get; set; }

        public int CommentCount { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    }
}
