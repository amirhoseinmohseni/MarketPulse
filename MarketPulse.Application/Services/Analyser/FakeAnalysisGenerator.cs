using MarketPulse.Domain.Entities;

namespace MarketPulse.Application.Services.Analyser
{
    public class FakeAnalysisGenerator : IAnalyser
    {
        public async Task<AnalysisResult> AnalyseRequest(Guid requestId, string idea, CancellationToken ct)
        {
            await Task.Delay(1500, ct); // simulate work

            return new AnalysisResult
            {
                Id = Guid.NewGuid(),
                AnalysisRequestId = requestId,
                MarketScore = Random.Shared.Next(40, 95),
                Summary = $"Fake summary for idea: {idea}",
                Strengths = "Fast to build; Clear audience; Differentiation potential",
                Weaknesses = "Unvalidated demand; Competition risk",
                Opportunities = "Niche targeting; Partnerships",
                Risks = "Low retention; Pricing pressure"
            };
        }
    }
}
