using MarketPulse.Domain.Enums;

namespace MarketPulse.Domain.Entities
{
    public class AnalysisInsight
    {
        public Guid Id { get; set; }

        public Guid AnalysisResultId { get; set; }
        public AnalysisResult? AnalysisResult { get; set; }

        public InsightType Type { get; set; }

        public int Position { get; set; }

        public string Text { get; set; } = string.Empty;

        public List<AnalysisEvidence> Evidence { get; set; } = new();
    }
}
