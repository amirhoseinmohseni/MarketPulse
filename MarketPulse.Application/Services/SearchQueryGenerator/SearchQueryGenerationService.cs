using MarketPulse.Domain.Repositories;

namespace MarketPulse.Application.Services.SearchQueryGenerator
{
    public class SearchQueryGenerationService : ISearchQueryGenerationService
    {
        private readonly ISearchQueryGenerator _searchQueryGenerator;
        private readonly ISearchQueryRepository _searchQueryRepository;

        public SearchQueryGenerationService(
            ISearchQueryGenerator searchQueryGenerator,
            ISearchQueryRepository searchQueryRepository)
        {
            _searchQueryGenerator = searchQueryGenerator;
            _searchQueryRepository = searchQueryRepository;
        }

        public async Task GenerateForAnalysisRequestAsync(
            Guid analysisRequestId,
            string idea,
            CancellationToken cancellationToken = default)
        {
            var searchQueries = await _searchQueryGenerator.GenerateAsync(idea, cancellationToken);

            foreach (var searchQuery in searchQueries)
            {
                searchQuery.AnalysisRequestId = analysisRequestId;
            }

            await _searchQueryRepository.AddRangeAsync(searchQueries, cancellationToken);
            await _searchQueryRepository.SaveChangesAsync(cancellationToken);
        }
    }
}
