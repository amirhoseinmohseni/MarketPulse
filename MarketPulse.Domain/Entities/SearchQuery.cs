using MarketPulse.Domain.Enums;

namespace MarketPulse.Domain.Entities
{
    public class SearchQuery
    {
        public Guid Id { get; set; }

        public Guid AnalysisRequestId { get; set; }
        public AnalysisRequest? Request { get; set; }


        public string Query { get; init; } = default!;

        public QueryCategory Category { get; init; }

        public int Priority { get; init; }
    }
}
