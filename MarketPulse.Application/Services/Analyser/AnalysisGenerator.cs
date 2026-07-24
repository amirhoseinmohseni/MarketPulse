using MarketPulse.Domain.Entities;

namespace MarketPulse.Application.Services.Analyser
{
    public class AnalysisGenerator : IAnalysisGenerator
    {
        public async Task<AnalysisResult> AnalyseRequest(Guid requestId, string idea, CancellationToken ct)
        {
            await Task.Delay(1500, ct); // simulate work

            return new AnalysisResult
            {
                Id = Guid.NewGuid(),
                AnalysisRequestId = requestId,
                MarketScore = null,
                Summary = "Evidence-based market insight generation is not implemented yet."
            };
        }
    }
}
