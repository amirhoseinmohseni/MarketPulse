using MarketPulse.Domain.Entities;

namespace MarketPulse.Domain.Repositories
{
    public interface ICollectedMarketItemRepository
    {
        Task AddRangeAsync(IEnumerable<CollectedMarketItem> items, CancellationToken ct = default);
        Task<IReadOnlyList<CollectedMarketItem>> GetByAnalysisRequestIdAsync(Guid analysisRequestId, CancellationToken ct = default);
        Task<bool> ExistsForAnalysisRequestAsync(Guid analysisRequestId, string source, string externalId, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
