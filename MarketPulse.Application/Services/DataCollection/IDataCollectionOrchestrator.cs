using MarketPulse.Domain.Entities;

namespace MarketPulse.Application.Services.DataCollection
{
    public interface IDataCollectionOrchestrator
    {
        Task<IReadOnlyList<DataCollectionResult>> CollectAsync(
            Guid analysisRequestId,
            string idea,
            IReadOnlyCollection<SearchQuery> searchQueries,
            CancellationToken cancellationToken = default);
    }
}
