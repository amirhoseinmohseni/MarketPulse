namespace MarketPulse.Domain.Entities
{
    public class AnalysisResult
    {
        public Guid Id { get; set; }

        public Guid AnalysisRequestId { get; set; }
        public AnalysisRequest? Request { get; set; } 

        public int MarketScore { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string Strengths { get; set; } = string.Empty;
        public string Weaknesses { get; set; } = string.Empty;
        public string Opportunities { get; set; } = string.Empty;
        public string Risks { get; set; } = string.Empty;
    }
}
