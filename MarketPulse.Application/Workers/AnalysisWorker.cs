using MarketPulse.Application.Services.Analyser;
using MarketPulse.Application.Services.RedditDataCollection;
using MarketPulse.Domain.Enums;
using MarketPulse.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MarketPulse.Application.Workers
{
    public class AnalysisWorker : BackgroundService
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
                Guid requestId = Guid.Empty;
                try
                {
                    requestId = await _queue.DequeueAsync(stoppingToken);
                    _logger.LogInformation("Dequeued request {RequestId} for processing.", requestId);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Worker cancellation requested. Stopping...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while dequeuing a request.");
                    continue;
                }

                using (var scope = _scopeFactory.CreateScope())
                {
                    var requestRepository = scope.ServiceProvider.GetRequiredService<IAnalysisRequestRepository>();
                    var resultRepository = scope.ServiceProvider.GetRequiredService<IAnalysisResultRepository>();
                    var searchQueryRepository = scope.ServiceProvider.GetRequiredService<ISearchQueryRepository>();
                    var searchQueryGenerationService = scope.ServiceProvider.GetRequiredService<global::MarketPulse.Application.Services.SearchQueryGenerator.ISearchQueryGenerationService>();
                    var redditDataCollector = scope.ServiceProvider.GetRequiredService<IRedditDataCollector>();
                    var analyser = scope.ServiceProvider.GetRequiredService<IAnalysisGenerator>();

                    try
                    {
                        var req = await requestRepository.GetByIdAsync(requestId, stoppingToken);
                        if (req is null)
                        {
                            _logger.LogWarning("Request {RequestId} found in queue but not in database.", requestId);
                            continue;
                        }

                        if (req.Status is AnalysisStatus.Completed)
                        {
                            _logger.LogInformation("Request {RequestId} is already completed. Skipping.", requestId);
                            continue;
                        }

                        req.Status = AnalysisStatus.Processing;
                        await requestRepository.UpdateAsync(req, stoppingToken);
                        await requestRepository.SaveChangesAsync(stoppingToken);
                        _logger.LogDebug("Status updated to Processing for request {RequestId}.", requestId);

                        _logger.LogInformation("Generating search queries for request {RequestId}...", requestId);
                        await searchQueryGenerationService.GenerateForAnalysisRequestAsync(req.Id, req.Idea, stoppingToken);
                        _logger.LogInformation("Search queries generated and saved for request {RequestId}.", requestId);

                        _logger.LogInformation("Loading generated search queries for request {RequestId}...", requestId);
                        var searchQueries = await searchQueryRepository.GetByAnalysisRequestIdAsync(req.Id, stoppingToken);
                        _logger.LogInformation("Loaded {SearchQueryCount} search queries for request {RequestId}.", searchQueries.Count, requestId);

                        _logger.LogInformation("Collecting Reddit posts for request {RequestId}...", requestId);
                        await redditDataCollector.CollectForAnalysisRequestAsync(req.Id, searchQueries, stoppingToken);
                        _logger.LogInformation("Reddit post collection completed for request {RequestId}.", requestId);

                        _logger.LogInformation("Starting AI analysis for request {RequestId}...", requestId);
                        var result = await analyser.AnalyseRequest(req.Id, req.Idea, stoppingToken);

                        await resultRepository.AddAsync(result, stoppingToken);
                        req.Status = AnalysisStatus.Completed;
                        req.CompletedAt = DateTime.UtcNow;

                        await requestRepository.UpdateAsync(req, stoppingToken);
                        await resultRepository.SaveChangesAsync(stoppingToken);

                        _logger.LogInformation("Successfully completed analysis for request {RequestId}.", requestId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred during analysis of request {RequestId}.", requestId);

                        try
                        {
                            var req = await requestRepository.GetByIdAsync(requestId, stoppingToken);
                            if (req is not null)
                            {
                                req.Status = AnalysisStatus.Failed;
                                req.CompletedAt = DateTime.UtcNow;
                                await requestRepository.UpdateAsync(req, stoppingToken);
                                await requestRepository.SaveChangesAsync(stoppingToken);
                                _logger.LogInformation("Request {RequestId} has been marked as Failed in database.", requestId);
                            }
                        }
                        catch (Exception dbEx)
                        {
                            _logger.LogCritical(dbEx, "Failed to update status to 'Failed' for request {RequestId} in database.", requestId);
                        }
                    }
                }
            }

            _logger.LogInformation("Analysis Worker is shutting down.");
        }
    }
}
