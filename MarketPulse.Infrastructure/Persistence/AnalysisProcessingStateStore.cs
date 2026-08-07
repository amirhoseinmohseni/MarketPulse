using MarketPulse.Application.Services.AnalysisProcessing;
using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarketPulse.Infrastructure.Persistence;

public sealed class AnalysisProcessingStateStore : IAnalysisProcessingStateStore
{
    private readonly ApplicationDbContext _dbContext;

    public AnalysisProcessingStateStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AnalysisProcessingStartResult> TryStartAsync(
        Guid analysisRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await ReadStartStateAsync(
            analysisRequestId,
            cancellationToken);

        if (request is null)
        {
            return new AnalysisProcessingStartResult(
                AnalysisProcessingStartStatus.NotFound);
        }

        if (request.HasResult || request.Status == AnalysisStatus.Completed)
        {
            return new AnalysisProcessingStartResult(
                AnalysisProcessingStartStatus.AlreadyCompleted);
        }

        if (request.Status == AnalysisStatus.Processing)
        {
            return new AnalysisProcessingStartResult(
                AnalysisProcessingStartStatus.AlreadyProcessing);
        }

        var updated = await _dbContext.AnalysisRequests
            .Where(x => x.Id == analysisRequestId)
            .Where(x => x.Status == AnalysisStatus.Pending
                || x.Status == AnalysisStatus.Failed)
            .Where(x => x.Result == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, AnalysisStatus.Processing)
                    .SetProperty(x => x.CompletedAt, (DateTime?)null),
                cancellationToken);

        if (updated == 1)
        {
            return new AnalysisProcessingStartResult(
                AnalysisProcessingStartStatus.Started,
                new AnalysisProcessingRequest(request.Id, request.Idea));
        }

        var current = await ReadStartStateAsync(
            analysisRequestId,
            cancellationToken);

        return new AnalysisProcessingStartResult(
            current is null
                ? AnalysisProcessingStartStatus.NotFound
                : current.HasResult || current.Status == AnalysisStatus.Completed
                    ? AnalysisProcessingStartStatus.AlreadyCompleted
                    : AnalysisProcessingStartStatus.AlreadyProcessing);
    }

    public async Task<AnalysisCompletionStatus> CompleteAsync(
        Guid analysisRequestId,
        AnalysisResult result,
        DateTime completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.AnalysisRequestId != analysisRequestId)
        {
            throw new InvalidOperationException(
                "The generated result belongs to a different analysis request.");
        }

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             SELECT 1
             FROM "AnalysisRequests"
             WHERE "Id" = {analysisRequestId}
             FOR UPDATE
             """,
            cancellationToken);

        var request = await _dbContext.AnalysisRequests
            .SingleOrDefaultAsync(
                x => x.Id == analysisRequestId,
                cancellationToken);

        if (request is null)
        {
            return AnalysisCompletionStatus.NotFound;
        }

        var existingResult = await _dbContext.AnalysisResults
            .AnyAsync(
                x => x.AnalysisRequestId == analysisRequestId,
                cancellationToken);

        if (existingResult)
        {
            request.Status = AnalysisStatus.Completed;
            request.CompletedAt ??= completedAtUtc;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AnalysisCompletionStatus.AlreadyCompleted;
        }

        await ValidateEvidenceOwnershipAsync(
            analysisRequestId,
            result,
            cancellationToken);

        await _dbContext.AnalysisResults.AddAsync(result, cancellationToken);
        request.Status = AnalysisStatus.Completed;
        request.CompletedAt = completedAtUtc;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return AnalysisCompletionStatus.Completed;
    }

    public async Task MarkFailedAsync(
        Guid analysisRequestId,
        DateTime failedAtUtc,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ChangeTracker.Clear();

        await _dbContext.AnalysisRequests
            .Where(x => x.Id == analysisRequestId)
            .Where(x => x.Status != AnalysisStatus.Completed)
            .Where(x => x.Result == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, AnalysisStatus.Failed)
                    .SetProperty(x => x.CompletedAt, failedAtUtc),
                cancellationToken);
    }

    private Task<StartState?> ReadStartStateAsync(
        Guid analysisRequestId,
        CancellationToken cancellationToken)
        => _dbContext.AnalysisRequests
            .AsNoTracking()
            .Where(x => x.Id == analysisRequestId)
            .Select(x => new StartState(
                x.Id,
                x.Idea,
                x.Status,
                x.Result != null))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task ValidateEvidenceOwnershipAsync(
        Guid analysisRequestId,
        AnalysisResult result,
        CancellationToken cancellationToken)
    {
        var evidenceIds = result.Insights
            .SelectMany(x => x.Evidence)
            .Select(x => x.CollectedMarketItemId)
            .Distinct()
            .ToList();

        if (evidenceIds.Count == 0)
        {
            return;
        }

        var ownedEvidenceCount = await _dbContext.CollectedMarketItems
            .CountAsync(
                x => x.AnalysisRequestId == analysisRequestId
                    && evidenceIds.Contains(x.Id),
                cancellationToken);

        if (ownedEvidenceCount != evidenceIds.Count)
        {
            throw new InvalidOperationException(
                "Analysis evidence must reference collected items owned by the same request.");
        }
    }

    private sealed record StartState(
        Guid Id,
        string Idea,
        AnalysisStatus Status,
        bool HasResult);
}
