using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Repositories;
using MarketPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarketPulse.Infrastructure.Repositories
{
    public class RedditPostRepository : IRedditPostRepository
    {
        private readonly ApplicationDbContext _db;

        public RedditPostRepository(ApplicationDbContext db) => _db = db;

        public Task AddRangeAsync(IEnumerable<RedditPost> redditPosts, CancellationToken ct = default)
            => _db.RedditPosts.AddRangeAsync(redditPosts, ct);

        public async Task<IReadOnlyList<RedditPost>> GetByAnalysisRequestIdAsync(
            Guid analysisRequestId,
            CancellationToken ct = default)
            => await _db.RedditPosts
                .AsNoTracking()
                .Where(x => x.AnalysisRequestId == analysisRequestId)
                .ToListAsync(ct);

        public Task<bool> ExistsForAnalysisRequestAsync(
            Guid analysisRequestId,
            string redditPostId,
            CancellationToken ct = default)
            => _db.RedditPosts
                .AsNoTracking()
                .AnyAsync(x => x.AnalysisRequestId == analysisRequestId
                    && x.RedditPostId == redditPostId, ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}
