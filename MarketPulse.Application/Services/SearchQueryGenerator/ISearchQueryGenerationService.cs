namespace MarketPulse.Application.Services.SearchQueryGenerator
{
    public interface ISearchQueryGenerationService
    {
        Task GenerateForAnalysisRequestAsync(Guid analysisRequestId, string idea, CancellationToken cancellationToken = default);
    }
}
