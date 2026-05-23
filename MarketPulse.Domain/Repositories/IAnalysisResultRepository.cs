using MarketPulse.Domain.Entities;

namespace MarketPulse.Domain.Repositories
{
    public interface IAnalysisResultRepository
    {
        Task AddAsync(AnalysisResult result, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
