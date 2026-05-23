using MarketPulse.Domain.Entities;

namespace MarketPulse.Application.Services.Analysis
{
    public interface IAnalyser
    {
        Task<AnalysisResult> AnalyseRequest(Guid requestId, string idea, CancellationToken ct);
    }
}
