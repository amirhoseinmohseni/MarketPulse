using MarketPulse.Application.Services.Analyser;
using MarketPulse.Application.Services.DataCollection;
using MarketPulse.Application.Services.SearchQueryGenerator;
using MarketPulse.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace MarketPulse.Application.Services.AnalysisProcessing;

public sealed class AnalysisRequestProcessor : IAnalysisRequestProcessor
{
    private readonly IAnalysisProcessingStateStore _stateStore;
    private readonly ISearchQueryGenerationService _searchQueryGenerationService;
    private readonly ISearchQueryRepository _searchQueryRepository;
    private readonly IDataCollectionOrchestrator _dataCollectionOrchestrator;
    private readonly IAnalysisGenerator _analysisGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AnalysisRequestProcessor> _logger;

    public AnalysisRequestProcessor(
        IAnalysisProcessingStateStore stateStore,
        ISearchQueryGenerationService searchQueryGenerationService,
        ISearchQueryRepository searchQueryRepository,
        IDataCollectionOrchestrator dataCollectionOrchestrator,
        IAnalysisGenerator analysisGenerator,
        TimeProvider timeProvider,
        ILogger<AnalysisRequestProcessor> logger)
    {
        _stateStore = stateStore;
        _searchQueryGenerationService = searchQueryGenerationService;
        _searchQueryRepository = searchQueryRepository;
        _dataCollectionOrchestrator = dataCollectionOrchestrator;
        _analysisGenerator = analysisGenerator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AnalysisProcessingOutcome> ProcessAsync(
        Guid analysisRequestId,
        CancellationToken cancellationToken = default)
    {
        var start = await _stateStore.TryStartAsync(
            analysisRequestId,
            cancellationToken);

        switch (start.Status)
        {
            case AnalysisProcessingStartStatus.NotFound:
                _logger.LogWarning(
                    "Request {RequestId} was queued but no database row exists.",
                    analysisRequestId);
                return AnalysisProcessingOutcome.NotFound;

            case AnalysisProcessingStartStatus.AlreadyCompleted:
                _logger.LogInformation(
                    "Request {RequestId} already has a completed result. Skipping.",
                    analysisRequestId);
                return AnalysisProcessingOutcome.SkippedCompleted;

            case AnalysisProcessingStartStatus.AlreadyProcessing:
                _logger.LogInformation(
                    "Request {RequestId} is already processing. Skipping duplicate queue delivery.",
                    analysisRequestId);
                return AnalysisProcessingOutcome.SkippedProcessing;

            case AnalysisProcessingStartStatus.Started:
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported processing start status: {start.Status}.");
        }

        var request = start.Request
            ?? throw new InvalidOperationException(
                "A started analysis must include its request data.");

        try
        {
            var searchQueries = await _searchQueryRepository
                .GetByAnalysisRequestIdAsync(request.Id, cancellationToken);

            if (searchQueries.Count == 0)
            {
                await _searchQueryGenerationService.GenerateForAnalysisRequestAsync(
                    request.Id,
                    request.Idea,
                    cancellationToken);

                searchQueries = await _searchQueryRepository
                    .GetByAnalysisRequestIdAsync(request.Id, cancellationToken);
            }

            await _dataCollectionOrchestrator.CollectAsync(
                request.Id,
                request.Idea,
                searchQueries,
                cancellationToken);

            var result = await _analysisGenerator.AnalyseRequest(
                request.Id,
                request.Idea,
                cancellationToken);

            var completion = await _stateStore.CompleteAsync(
                request.Id,
                result,
                _timeProvider.GetUtcNow().UtcDateTime,
                cancellationToken);

            return completion switch
            {
                AnalysisCompletionStatus.Completed => AnalysisProcessingOutcome.Completed,
                AnalysisCompletionStatus.AlreadyCompleted => AnalysisProcessingOutcome.SkippedCompleted,
                AnalysisCompletionStatus.NotFound => AnalysisProcessingOutcome.NotFound,
                _ => throw new InvalidOperationException(
                    $"Unsupported completion status: {completion}.")
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Processing request {RequestId} was cancelled by host shutdown.",
                analysisRequestId);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Analysis processing failed for request {RequestId}.",
                analysisRequestId);

            cancellationToken.ThrowIfCancellationRequested();

            await _stateStore.MarkFailedAsync(
                analysisRequestId,
                _timeProvider.GetUtcNow().UtcDateTime,
                cancellationToken);

            return AnalysisProcessingOutcome.Failed;
        }
    }
}
