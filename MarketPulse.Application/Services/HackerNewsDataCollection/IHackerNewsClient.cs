namespace MarketPulse.Application.Services.HackerNewsDataCollection
{
    public interface IHackerNewsClient
    {
        Task<IReadOnlyList<HackerNewsSearchResult>> SearchAsync(
            HackerNewsSearchRequest request,
            CancellationToken cancellationToken = default);
    }
}
