namespace MarketPulse.Application.Services.DataCollection
{
    public class DataCollectorOptions
    {
        public const string SectionName = "DataCollectors";

        public bool FailFast { get; init; }

        public Dictionary<string, DataCollectorSourceOptions> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class DataCollectorSourceOptions
    {
        public bool Enabled { get; init; }
    }
}
