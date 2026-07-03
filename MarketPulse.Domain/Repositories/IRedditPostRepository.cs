using MarketPulse.Domain.Entities;

namespace MarketPulse.Domain.Repositories
{
    public interface IRedditPostRepository
    {
        Task AddRangeAsync(IEnumerable<RedditPost> redditPosts, CancellationToken ct = default);
        Task<IReadOnlyList<RedditPost>> GetByAnalysisRequestIdAsync(Guid analysisRequestId, CancellationToken ct = default);
        Task<bool> ExistsForAnalysisRequestAsync(Guid analysisRequestId, string redditPostId, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
