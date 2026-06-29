using System.Text.Json.Serialization;

namespace MarketPulse.Infrastructure.Reddit
{
    internal sealed class RedditListingResponse
    {
        [JsonPropertyName("data")]
        public RedditListingData? Data { get; init; }
    }

    internal sealed class RedditListingData
    {
        [JsonPropertyName("children")]
        public List<RedditChild> Children { get; init; } = new();
    }

    internal sealed class RedditChild
    {
        [JsonPropertyName("data")]
        public RedditPostData? Data { get; init; }
    }

    internal sealed class RedditPostData
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("subreddit")]
        public string Subreddit { get; init; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("selftext")]
        public string? SelfText { get; init; }

        [JsonPropertyName("url")]
        public string? Url { get; init; }

        [JsonPropertyName("permalink")]
        public string Permalink { get; init; } = string.Empty;

        [JsonPropertyName("score")]
        public int Score { get; init; }

        [JsonPropertyName("num_comments")]
        public int CommentCount { get; init; }

        [JsonPropertyName("created_utc")]
        public double CreatedUtc { get; init; }
    }
}
