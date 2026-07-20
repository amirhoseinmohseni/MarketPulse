namespace MarketPulse.Application.Services.DataCollection
{
    public class DataCollectionResult
    {
        public string SourceName { get; init; } = string.Empty;

        public int ItemsCollected { get; init; }

        public bool Succeeded { get; init; }

        public string? ErrorMessage { get; init; }
    }
}
