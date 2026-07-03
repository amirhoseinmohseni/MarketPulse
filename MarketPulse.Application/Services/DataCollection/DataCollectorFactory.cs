namespace MarketPulse.Application.Services.DataCollection
{
    public class DataCollectorFactory : IDataCollectorFactory
    {
        private readonly IEnumerable<IDataCollector> _collectors;
        private readonly DataCollectorOptions _options;

        public DataCollectorFactory(
            IEnumerable<IDataCollector> collectors,
            DataCollectorOptions options)
        {
            _collectors = collectors;
            _options = options;
        }

        public IReadOnlyList<IDataCollector> GetEnabledCollectors()
            => _collectors
                .Where(x => _options.Sources.TryGetValue(x.SourceName, out var sourceOptions)
                    && sourceOptions.Enabled)
                .ToList();
    }
}
