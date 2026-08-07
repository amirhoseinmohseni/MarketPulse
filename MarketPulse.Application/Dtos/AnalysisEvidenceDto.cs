namespace MarketPulse.Application.Dtos
{
    public class AnalysisEvidenceDto
    {
        public Guid CollectedMarketItemId { get; set; }

        public string Source { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? Url { get; set; }

        public string? Permalink { get; set; }

        public string Reason { get; set; } = string.Empty;
    }
}
