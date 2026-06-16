using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Repositories;
using MarketPulse.Infrastructure.Persistence;

namespace MarketPulse.Infrastructure.Repositories
{
    public class SearchQueryRepository : ISearchQueryRepository
    {
        private readonly ApplicationDbContext _db;

        public SearchQueryRepository(ApplicationDbContext db) => _db = db;

        public Task AddRangeAsync(IEnumerable<SearchQuery> searchQueries, CancellationToken ct = default)
            => _db.SearchQueries.AddRangeAsync(searchQueries, ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}
