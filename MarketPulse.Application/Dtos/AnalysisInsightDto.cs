namespace MarketPulse.Application.Dtos
{
    public class AnalysisInsightDto
    {
        public Guid Id { get; set; }

        public string Text { get; set; } = string.Empty;

        public List<Guid> EvidenceIds { get; set; } = new();
    }
}
