namespace MarketPulse.Infrastructure.Reddit
{
    public interface IRedditAccessTokenProvider
    {
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    }
}
