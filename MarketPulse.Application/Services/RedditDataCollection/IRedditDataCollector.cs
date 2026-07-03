using MarketPulse.Domain.Entities;

namespace MarketPulse.Application.Services.RedditDataCollection
{
    public interface IRedditDataCollector
    {
        Task CollectForAnalysisRequestAsync(
            Guid analysisRequestId,
            IReadOnlyCollection<SearchQuery> searchQueries,
            CancellationToken cancellationToken = default);
    }
}
