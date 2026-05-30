using MarketPulse.Domain.Entities;

namespace MarketPulse.Application.Services.SearchQueryGenerator
{
    public interface ISearchQueryGenerator
    {
        Task<List<SearchQuery>> GenerateAsync(
            string idea,
            CancellationToken cancellationToken = default);
    }
}
