using MarketPulse.Application.Services.DataCollection;
using MarketPulse.Application.Services.RedditDataCollection;
using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Repositories;

namespace MarketPulse.Infrastructure.Reddit
{
    public sealed class RedditDataCollector : IDataCollector
    {
        public string SourceName => "Reddit";

        private readonly IRedditClient _redditClient;
        private readonly ICollectedMarketItemRepository _collectedMarketItemRepository;
        private readonly RedditOptions _options;

        public RedditDataCollector(
            IRedditClient redditClient,
            ICollectedMarketItemRepository collectedMarketItemRepository,
            RedditOptions options)
        {
            _redditClient = redditClient;
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
            var seenPostIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var collectedItems = new List<CollectedMarketItem>();

            foreach (var searchQuery in context.SearchQueries)
            {
                var results = await _redditClient.SearchPostsAsync(
                    new RedditPostSearchRequest
                    {
                        Query = searchQuery.Query,
                        Limit = _options.DefaultLimit
                    },
                    cancellationToken);

                foreach (var result in results)
                {
                    if (string.IsNullOrWhiteSpace(result.RedditPostId)
                        || !seenPostIds.Add(result.RedditPostId)
                        || await _collectedMarketItemRepository.ExistsForAnalysisRequestAsync(
                            context.AnalysisRequestId,
                            SourceName,
                            result.RedditPostId,
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
                        ExternalId = result.RedditPostId,
                        Title = result.Title,
                        Content = result.SelfText,
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
