namespace MarketPulse.Application
{
    public interface IBackgroundTaskQueue
    {
        ValueTask QueueBackgroundWorkItemAsync(Guid requestId);
        ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
    }
}
