using MarketPulse.Domain.Entities;

namespace MarketPulse.Application.Services.Analyser
{
    public interface IAnalysisGenerator
    {
        Task<AnalysisResult> AnalyseRequest(Guid requestId, string idea, CancellationToken ct);
    }
}
