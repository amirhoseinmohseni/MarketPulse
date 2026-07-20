using MarketPulse.Domain.Entities;

namespace MarketPulse.Application.Services.DataCollection
{
    public class DataCollectionContext
    {
        public Guid AnalysisRequestId { get; init; }

        public string Idea { get; init; } = string.Empty;

        public IReadOnlyCollection<SearchQuery> SearchQueries { get; init; } = Array.Empty<SearchQuery>();
    }
}
