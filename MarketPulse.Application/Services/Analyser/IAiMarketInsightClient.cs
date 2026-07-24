namespace MarketPulse.Application.Services.Analyser;

public interface IAiMarketInsightClient
{
    Task<string> GenerateAsync(
        AiMarketInsightRequest request,
        CancellationToken cancellationToken = default);
}
