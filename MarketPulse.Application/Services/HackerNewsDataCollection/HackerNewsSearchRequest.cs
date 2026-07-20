namespace MarketPulse.Application.Services.HackerNewsDataCollection
{
    public class HackerNewsSearchRequest
    {
        public string Query { get; init; } = string.Empty;

        public int Limit { get; init; } = 20;
    }
}
