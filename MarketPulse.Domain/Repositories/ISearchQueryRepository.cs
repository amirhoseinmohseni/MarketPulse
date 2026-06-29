using MarketPulse.Domain.Entities;

namespace MarketPulse.Domain.Repositories
{
    public interface ISearchQueryRepository
    {
        Task AddRangeAsync(IEnumerable<SearchQuery> searchQueries, CancellationToken ct = default);
        Task<IReadOnlyList<SearchQuery>> GetByAnalysisRequestIdAsync(Guid analysisRequestId, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
