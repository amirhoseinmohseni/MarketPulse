using MarketPulse.Application.Services.Analysis;
using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Enums;
using MarketPulse.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MarketPulse.Application.Workers
{
    public class AnalysisWorker : BackgroundService
    {
        private readonly IBackgroundTaskQueue _queue;
        //private readonly IAnalysisRequestRepository _requests;
        //private readonly IAnalysisResultRepository _results;
        //private readonly IAnalyser _analyser;
        private readonly IServiceScopeFactory _scopeFactory;

        public AnalysisWorker(
            IBackgroundTaskQueue queue, IServiceScopeFactory scopeFactory)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Guid requestId;
                try
                {
                    requestId = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                using (var scope = _scopeFactory.CreateScope())
                {
                    var requestRepository = scope.ServiceProvider.GetRequiredService<IAnalysisRequestRepository>();
                    var resultRepository = scope.ServiceProvider.GetRequiredService<IAnalysisResultRepository>();
                    var analyser = scope.ServiceProvider.GetRequiredService<IAnalyser>();
                    try
                    {



                        var req = await requestRepository.GetByIdAsync(requestId, stoppingToken);
                        if (req is null)
                        {
                            //_logger.LogWarning("AnalysisRequest not found. Id={Id}", requestId);
                            continue;
                        }

                        if (req.Status is AnalysisStatus.Completed)
                            continue;

                        req.Status = AnalysisStatus.Processing;
                        await requestRepository.UpdateAsync(req, stoppingToken);
                        await requestRepository.SaveChangesAsync(stoppingToken);

                        var result = await analyser.AnalyseRequest(req.Id, req.Idea, stoppingToken);

                        await resultRepository.AddAsync(result, stoppingToken);

                        req.Status = AnalysisStatus.Completed;
                        req.CompletedAt = DateTime.UtcNow;

                        await requestRepository.UpdateAsync(req, stoppingToken);

                        await resultRepository.SaveChangesAsync(stoppingToken);

                        //_logger.LogInformation("Analysis completed. Id={Id}", requestId);


                    }
                    catch (Exception ex)
                    {
                        //_logger.LogError(ex, "Analysis failed. Id={Id}", requestId);

                        var req = await requestRepository.GetByIdAsync(requestId, stoppingToken);
                        if (req is not null)
                        {
                            req.Status = AnalysisStatus.Failed;
                            req.CompletedAt = DateTime.UtcNow;
                            await requestRepository.UpdateAsync(req, stoppingToken);
                            await requestRepository.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
            }
        }
    }
}