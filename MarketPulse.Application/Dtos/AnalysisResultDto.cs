using MarketPulse.Domain.Enums;

namespace MarketPulse.Application.Dtos
{
    public class AnalysisResultDto
    {
        public Guid Id { get; set; }

        public Guid AnalysisRequestId { get; set; }

        public int? MarketScore { get; set; }
        public SignalStrength SignalStrength { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<AnalysisInsightDto> Strengths { get; set; } = new();
        public List<AnalysisInsightDto> Weaknesses { get; set; } = new();
        public List<AnalysisInsightDto> Opportunities { get; set; } = new();
        public List<AnalysisInsightDto> Risks { get; set; } = new();
        public List<AnalysisEvidenceDto> Evidence { get; set; } = new();
    }
}
