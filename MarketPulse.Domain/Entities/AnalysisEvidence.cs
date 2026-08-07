namespace MarketPulse.Domain.Entities
{
    public class AnalysisEvidence
    {
        public Guid Id { get; set; }

        public Guid AnalysisInsightId { get; set; }
        public AnalysisInsight? AnalysisInsight { get; set; }

        public Guid CollectedMarketItemId { get; set; }
        public CollectedMarketItem? CollectedMarketItem { get; set; }
    }
}
