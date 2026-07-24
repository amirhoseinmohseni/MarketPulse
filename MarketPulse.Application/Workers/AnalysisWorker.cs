using MarketPulse.Application.Services.AnalysisProcessing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MarketPulse.Application.Workers;

public sealed class AnalysisWorker : BackgroundService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalysisWorker> _logger;

    public AnalysisWorker(
        IBackgroundTaskQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AnalysisWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Analysis Worker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid requestId;
            try
            {
                requestId = await _queue.DequeueAsync(stoppingToken);
                _logger.LogInformation(
                    "Dequeued request {RequestId} for processing.",
                    requestId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Error occurred while dequeuing an analysis request.");
                continue;
            }

            using var scope = _scopeFactory.CreateScope();
            try
            {
                var processor = scope.ServiceProvider
                    .GetRequiredService<IAnalysisRequestProcessor>();
                var outcome = await processor.ProcessAsync(requestId, stoppingToken);

                _logger.LogInformation(
                    "Finished queue item {RequestId} with outcome {Outcome}.",
                    requestId,
                    outcome);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Worker shutdown cancelled processing request {RequestId}.",
                    requestId);
                break;
            }
            catch (Exception exception)
            {
                _logger.LogCritical(
                    exception,
                    "Unexpected worker failure while processing request {RequestId}.",
                    requestId);
            }
        }

        _logger.LogInformation("Analysis Worker is shutting down.");
    }
}
