using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Repositories;
using MarketPulse.Infrastructure.Persistence;

namespace MarketPulse.Infrastructure.Repositories
{
    public class AnalysisResultRepository : IAnalysisResultRepository
    {
        private readonly ApplicationDbContext _db;

        public AnalysisResultRepository(ApplicationDbContext db) => _db = db;

        public Task AddAsync(AnalysisResult result, CancellationToken ct = default)
            => _db.AnalysisResults.AddAsync(result, ct).AsTask();

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}
