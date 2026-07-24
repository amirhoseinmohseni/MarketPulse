using MarketPulse.Domain.Entities;

namespace MarketPulse.Application.Services.AnalysisProcessing;

public interface IAnalysisProcessingStateStore
{
    Task<AnalysisProcessingStartResult> TryStartAsync(
        Guid analysisRequestId,
        CancellationToken cancellationToken = default);

    Task<AnalysisCompletionStatus> CompleteAsync(
        Guid analysisRequestId,
        AnalysisResult result,
        DateTime completedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid analysisRequestId,
        DateTime failedAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed record AnalysisProcessingRequest(Guid Id, string Idea);

public sealed record AnalysisProcessingStartResult(
    AnalysisProcessingStartStatus Status,
    AnalysisProcessingRequest? Request = null);

public enum AnalysisProcessingStartStatus
{
    Started,
    NotFound,
    AlreadyCompleted,
    AlreadyProcessing
}

public enum AnalysisCompletionStatus
{
    Completed,
    AlreadyCompleted,
    NotFound
}
