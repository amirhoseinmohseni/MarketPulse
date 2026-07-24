using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Repositories;
using MarketPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarketPulse.Infrastructure.Repositories
{
    public class AnalysisRequestRepository : IAnalysisRequestRepository
    {
        private readonly ApplicationDbContext _db;

        public AnalysisRequestRepository(ApplicationDbContext db) => _db = db;

        public Task AddAsync(AnalysisRequest request, CancellationToken ct = default)
            => _db.AnalysisRequests.AddAsync(request, ct).AsTask();

        public Task<AnalysisRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _db.AnalysisRequests.FirstOrDefaultAsync(x => x.Id == id, ct);

        public Task<AnalysisRequest?> GetByIdWithResultAsync(Guid id, CancellationToken ct = default)
            => _db.AnalysisRequests
                .Include(x => x.Result)
                    .ThenInclude(x => x!.Insights)
                        .ThenInclude(x => x.Evidence)
                            .ThenInclude(x => x.CollectedMarketItem)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == id, ct);

        public Task UpdateAsync(AnalysisRequest request, CancellationToken ct = default)
        {
            _db.AnalysisRequests.Update(request);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}
