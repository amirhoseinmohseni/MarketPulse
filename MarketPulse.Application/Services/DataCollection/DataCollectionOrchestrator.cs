using MarketPulse.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MarketPulse.Application.Services.DataCollection
{
    public class DataCollectionOrchestrator : IDataCollectionOrchestrator
    {
        private readonly IDataCollectorFactory _collectorFactory;
        private readonly DataCollectorOptions _options;
        private readonly ILogger<DataCollectionOrchestrator> _logger;

        public DataCollectionOrchestrator(
            IDataCollectorFactory collectorFactory,
            DataCollectorOptions options,
            ILogger<DataCollectionOrchestrator> logger)
        {
            _collectorFactory = collectorFactory;
            _options = options;
            _logger = logger;
        }

        public async Task<IReadOnlyList<DataCollectionResult>> CollectAsync(
            Guid analysisRequestId,
            string idea,
            IReadOnlyCollection<SearchQuery> searchQueries,
            CancellationToken cancellationToken = default)
        {
            var collectors = _collectorFactory.GetEnabledCollectors();
            var context = new DataCollectionContext
            {
                AnalysisRequestId = analysisRequestId,
                Idea = idea,
                SearchQueries = searchQueries
            };
            var results = new List<DataCollectionResult>();

            _logger.LogInformation("Running {CollectorCount} enabled data collectors for request {RequestId}.", collectors.Count, analysisRequestId);

            foreach (var collector in collectors)
            {
                try
                {
                    _logger.LogInformation("Starting data collector {SourceName} for request {RequestId}.", collector.SourceName, analysisRequestId);
                    var result = await collector.CollectAsync(context, cancellationToken);
                    results.Add(result);
                    _logger.LogInformation(
                        "Data collector {SourceName} completed for request {RequestId}. Succeeded: {Succeeded}. Items collected: {ItemsCollected}.",
                        result.SourceName,
                        analysisRequestId,
                        result.Succeeded,
                        result.ItemsCollected);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Data collector {SourceName} failed for request {RequestId}.", collector.SourceName, analysisRequestId);

                    if (_options.FailFast)
                    {
                        throw;
                    }

                    results.Add(new DataCollectionResult
                    {
                        SourceName = collector.SourceName,
                        ItemsCollected = 0,
                        Succeeded = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return results;
        }
    }
}
