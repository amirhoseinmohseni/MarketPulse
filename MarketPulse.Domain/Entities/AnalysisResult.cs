using MarketPulse.Domain.Enums;

namespace MarketPulse.Domain.Entities
{
    public class AnalysisResult
    {
        public Guid Id { get; set; }

        public Guid AnalysisRequestId { get; set; }
        public AnalysisRequest? Request { get; set; }

        public int? MarketScore { get; set; }
        public SignalStrength SignalStrength { get; set; } = SignalStrength.Weak;
        public string Summary { get; set; } = string.Empty;

        public List<AnalysisInsight> Insights { get; set; } = new();
    }
}
