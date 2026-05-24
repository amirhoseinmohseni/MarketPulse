using MarketPulse.Domain.Entities;

namespace MarketPulse.Application.Services.Analyser
{
    public interface IAnalyser
    {
        Task<AnalysisResult> AnalyseRequest(Guid requestId, string idea, CancellationToken ct);
    }
}
