namespace MarketPulse.Infrastructure.AI;

public interface IOpenRouterRetryDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class OpenRouterRetryDelay : IOpenRouterRetryDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        => Task.Delay(delay, cancellationToken);
}
