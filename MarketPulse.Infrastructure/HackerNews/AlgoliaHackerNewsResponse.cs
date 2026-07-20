using System.Text.Json.Serialization;

namespace MarketPulse.Infrastructure.HackerNews
{
    internal sealed class AlgoliaHackerNewsResponse
    {
        [JsonPropertyName("hits")]
        public List<AlgoliaHackerNewsHit> Hits { get; init; } = new();
    }

    internal sealed class AlgoliaHackerNewsHit
    {
        [JsonPropertyName("objectID")]
        public string ObjectId { get; init; } = string.Empty;

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("story_title")]
        public string? StoryTitle { get; init; }

        [JsonPropertyName("url")]
        public string? Url { get; init; }

        [JsonPropertyName("story_url")]
        public string? StoryUrl { get; init; }

        [JsonPropertyName("comment_text")]
        public string? CommentText { get; init; }

        [JsonPropertyName("story_text")]
        public string? StoryText { get; init; }

        [JsonPropertyName("points")]
        public int? Points { get; init; }

        [JsonPropertyName("num_comments")]
        public int? CommentCount { get; init; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; init; }

        [JsonPropertyName("created_at_i")]
        public long? CreatedAtUnix { get; init; }
    }
}
