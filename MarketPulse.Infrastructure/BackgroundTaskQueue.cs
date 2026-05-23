using MarketPulse.Application;
using System.Threading.Channels;

namespace MarketPulse.Infrastructure
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();

        public async ValueTask QueueBackgroundWorkItemAsync(Guid requestId)
            => await _queue.Writer.WriteAsync(requestId);

        public async ValueTask<Guid> DequeueAsync(CancellationToken ct)
            => await _queue.Reader.ReadAsync(ct);
    }
}
