using MarketPulse.Application.Services.RedditDataCollection;
using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Repositories;

namespace MarketPulse.Infrastructure.Reddit
{
    public sealed class RedditDataCollector : IRedditDataCollector
    {
        private readonly IRedditClient _redditClient;
        private readonly IRedditPostRepository _redditPostRepository;
        private readonly RedditOptions _options;

        public RedditDataCollector(
            IRedditClient redditClient,
            IRedditPostRepository redditPostRepository,
            RedditOptions options)
        {
            _redditClient = redditClient;
            _redditPostRepository = redditPostRepository;
            _options = options;
        }

        public async Task CollectForAnalysisRequestAsync(
            Guid analysisRequestId,
            IReadOnlyCollection<SearchQuery> searchQueries,
            CancellationToken cancellationToken = default)
        {
            if (searchQueries.Count == 0)
            {
                return;
            }

            var collectedAt = DateTime.UtcNow;
            var seenPostIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var redditPosts = new List<RedditPost>();

            foreach (var searchQuery in searchQueries)
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
                        || await _redditPostRepository.ExistsForAnalysisRequestAsync(
                            analysisRequestId,
                            result.RedditPostId,
                            cancellationToken))
                    {
                        continue;
                    }

                    redditPosts.Add(new RedditPost
                    {
                        Id = Guid.NewGuid(),
                        AnalysisRequestId = analysisRequestId,
                        SearchQueryId = searchQuery.Id,
                        RedditPostId = result.RedditPostId,
                        Subreddit = result.Subreddit,
                        Title = result.Title,
                        SelfText = result.SelfText,
                        Url = result.Url,
                        Permalink = result.Permalink,
                        Score = result.Score,
                        CommentCount = result.CommentCount,
                        CreatedUtc = result.CreatedUtc,
                        CollectedAt = collectedAt
                    });
                }
            }

            if (redditPosts.Count == 0)
            {
                return;
            }

            await _redditPostRepository.AddRangeAsync(redditPosts, cancellationToken);
            await _redditPostRepository.SaveChangesAsync(cancellationToken);
        }
    }
}
