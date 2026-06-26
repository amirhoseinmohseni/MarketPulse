using MarketPulse.Domain.Enums;

namespace MarketPulse.Domain.Entities
{
    public class AnalysisRequest
    {
        public Guid Id { get; set; }
        public string Idea { get; set; } = string.Empty;
        public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public AnalysisResult? Result { get; set; }
        public List<SearchQuery> SearchQueries { get; set; } = new List<SearchQuery>();
        public List<RedditPost> RedditPosts { get; set; } = new List<RedditPost>();
    }
}
