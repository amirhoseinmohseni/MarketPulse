namespace MarketPulse.Application.Services.AnalysisProcessing;

public interface IAnalysisRequestProcessor
{
    Task<AnalysisProcessingOutcome> ProcessAsync(
        Guid analysisRequestId,
        CancellationToken cancellationToken = default);
}

public enum AnalysisProcessingOutcome
{
    Completed,
    Failed,
    NotFound,
    SkippedCompleted,
    SkippedProcessing
}
