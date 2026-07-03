using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Repositories;
using MarketPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarketPulse.Infrastructure.Repositories
{
    public class CollectedMarketItemRepository : ICollectedMarketItemRepository
    {
        private readonly ApplicationDbContext _db;

        public CollectedMarketItemRepository(ApplicationDbContext db) => _db = db;

        public Task AddRangeAsync(IEnumerable<CollectedMarketItem> items, CancellationToken ct = default)
            => _db.CollectedMarketItems.AddRangeAsync(items, ct);

        public async Task<IReadOnlyList<CollectedMarketItem>> GetByAnalysisRequestIdAsync(
            Guid analysisRequestId,
            CancellationToken ct = default)
            => await _db.CollectedMarketItems
                .AsNoTracking()
                .Where(x => x.AnalysisRequestId == analysisRequestId)
                .ToListAsync(ct);

        public Task<bool> ExistsForAnalysisRequestAsync(
            Guid analysisRequestId,
            string source,
            string externalId,
            CancellationToken ct = default)
            => _db.CollectedMarketItems
                .AsNoTracking()
                .AnyAsync(x => x.AnalysisRequestId == analysisRequestId
                    && x.Source == source
                    && x.ExternalId == externalId, ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}
