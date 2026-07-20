using MarketPulse.Application.Services.DataCollection;
using MarketPulse.Application.Services.HackerNewsDataCollection;
using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Repositories;

namespace MarketPulse.Infrastructure.HackerNews
{
    public sealed class HackerNewsDataCollector : IDataCollector
    {
        public string SourceName => "HackerNews";

        private readonly IHackerNewsClient _hackerNewsClient;
        private readonly ICollectedMarketItemRepository _collectedMarketItemRepository;
        private readonly HackerNewsOptions _options;

        public HackerNewsDataCollector(
            IHackerNewsClient hackerNewsClient,
            ICollectedMarketItemRepository collectedMarketItemRepository,
            HackerNewsOptions options)
        {
            _hackerNewsClient = hackerNewsClient;
            _collectedMarketItemRepository = collectedMarketItemRepository;
            _options = options;
        }

        public async Task<DataCollectionResult> CollectAsync(
            DataCollectionContext context,
            CancellationToken cancellationToken = default)
        {
            if (context.SearchQueries.Count == 0)
            {
                return new DataCollectionResult
                {
                    SourceName = SourceName,
                    ItemsCollected = 0,
                    Succeeded = true
                };
            }

            var collectedAt = DateTime.UtcNow;
            var seenItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var collectedItems = new List<CollectedMarketItem>();

            foreach (var searchQuery in context.SearchQueries)
            {
                var results = await _hackerNewsClient.SearchAsync(
                    new HackerNewsSearchRequest
                    {
                        Query = searchQuery.Query,
                        Limit = _options.DefaultLimit
                    },
                    cancellationToken);

                foreach (var result in results)
                {
                    if (string.IsNullOrWhiteSpace(result.ExternalId)
                        || !seenItemIds.Add(result.ExternalId)
                        || await _collectedMarketItemRepository.ExistsForAnalysisRequestAsync(
                            context.AnalysisRequestId,
                            SourceName,
                            result.ExternalId,
                            cancellationToken))
                    {
                        continue;
                    }

                    collectedItems.Add(new CollectedMarketItem
                    {
                        Id = Guid.NewGuid(),
                        AnalysisRequestId = context.AnalysisRequestId,
                        SearchQueryId = searchQuery.Id,
                        Source = SourceName,
                        ExternalId = result.ExternalId,
                        Title = result.Title,
                        Content = result.Content,
                        Url = result.Url,
                        Permalink = result.Permalink,
                        Score = result.Score,
                        CommentCount = result.CommentCount,
                        CreatedUtc = result.CreatedUtc,
                        CollectedAt = collectedAt
                    });
                }
            }

            if (collectedItems.Count > 0)
            {
                await _collectedMarketItemRepository.AddRangeAsync(collectedItems, cancellationToken);
                await _collectedMarketItemRepository.SaveChangesAsync(cancellationToken);
            }

            return new DataCollectionResult
            {
                SourceName = SourceName,
                ItemsCollected = collectedItems.Count,
                Succeeded = true
            };
        }
    }
}
