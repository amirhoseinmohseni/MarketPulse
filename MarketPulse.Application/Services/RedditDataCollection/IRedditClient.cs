namespace MarketPulse.Application.Services.RedditDataCollection
{
    public interface IRedditClient
    {
        Task<IReadOnlyList<RedditPostSearchResult>> SearchPostsAsync(
            RedditPostSearchRequest request,
            CancellationToken cancellationToken = default);
    }
}
