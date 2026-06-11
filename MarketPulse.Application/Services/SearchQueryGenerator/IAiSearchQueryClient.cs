namespace MarketPulse.Application.Services.SearchQueryGenerator
{
    public interface IAiSearchQueryClient
    {
        Task<string> GenerateAsync(
            AiSearchQueryPrompt prompt,
            CancellationToken cancellationToken = default);
    }
}
