namespace MarketPulse.Application.Services.HackerNewsDataCollection
{
    public class HackerNewsSearchResult
    {
        public string ExternalId { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string? Content { get; init; }

        public string? Url { get; init; }

        public string Permalink { get; init; } = string.Empty;

        public int? Score { get; init; }

        public int? CommentCount { get; init; }

        public DateTime? CreatedUtc { get; init; }
    }
}
