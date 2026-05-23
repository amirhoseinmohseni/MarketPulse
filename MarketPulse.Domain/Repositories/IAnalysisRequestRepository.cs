using MarketPulse.Domain.Entities;

namespace MarketPulse.Domain.Repositories
{
    public interface IAnalysisRequestRepository
    {
        Task<AnalysisRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<AnalysisRequest?> GetByIdWithResultAsync(Guid id, CancellationToken ct = default);
        Task AddAsync(AnalysisRequest request, CancellationToken ct = default);
        Task UpdateAsync(AnalysisRequest request, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
