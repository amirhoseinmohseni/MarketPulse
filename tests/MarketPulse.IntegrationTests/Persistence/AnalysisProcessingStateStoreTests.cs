using MarketPulse.Application.Services.AnalysisProcessing;
using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Enums;
using MarketPulse.Infrastructure.Persistence;
using MarketPulse.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MarketPulse.IntegrationTests.Persistence;

public sealed class AnalysisProcessingStateStoreTests(PostgreSqlIntegrationFixture fixture)
    : PostgreSqlIntegrationTestBase(fixture)
{
    [Fact]
    public async Task TryStartAsync_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        await using var dbContext = Fixture.CreateDbContext();
        var store = new AnalysisProcessingStateStore(dbContext);

        var result = await store.TryStartAsync(Guid.NewGuid());

        Assert.Equal(AnalysisProcessingStartStatus.NotFound, result.Status);
        Assert.Null(result.Request);
    }

    [Fact]
    public async Task TryStartAsync_WhenPending_ClaimsRequestAndPersistsProcessing()
    {
        var request = IntegrationTestData.CreateRequest(AnalysisStatus.Pending);
        await SeedAsync(request);

        await using (var dbContext = Fixture.CreateDbContext())
        {
            var result = await new AnalysisProcessingStateStore(dbContext)
                .TryStartAsync(request.Id);

            Assert.Equal(AnalysisProcessingStartStatus.Started, result.Status);
            Assert.Equal(request.Id, result.Request?.Id);
            Assert.Equal(request.Idea, result.Request?.Idea);
        }

        var persisted = await ReadRequestAsync(request.Id);
        Assert.Equal(AnalysisStatus.Processing, persisted.Status);
        Assert.Null(persisted.CompletedAt);
    }

    [Fact]
    public async Task TryStartAsync_WhenFailed_ReclaimsRequestAndClearsCompletedAt()
    {
        var failedAt = new DateTime(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc);
        var request = IntegrationTestData.CreateRequest(AnalysisStatus.Failed, failedAt);
        await SeedAsync(request);

        await using (var dbContext = Fixture.CreateDbContext())
        {
            var result = await new AnalysisProcessingStateStore(dbContext)
                .TryStartAsync(request.Id);

            Assert.Equal(AnalysisProcessingStartStatus.Started, result.Status);
        }

        var persisted = await ReadRequestAsync(request.Id);
        Assert.Equal(AnalysisStatus.Processing, persisted.Status);
        Assert.Null(persisted.CompletedAt);
    }

    [Theory]
    [InlineData(AnalysisStatus.Processing, AnalysisProcessingStartStatus.AlreadyProcessing)]
    [InlineData(AnalysisStatus.Completed, AnalysisProcessingStartStatus.AlreadyCompleted)]
    public async Task TryStartAsync_WhenStatusCannotBeClaimed_ReturnsExpectedStatus(
        AnalysisStatus status,
        AnalysisProcessingStartStatus expected)
    {
        var request = IntegrationTestData.CreateRequest(status);
        await SeedAsync(request);

        await using var dbContext = Fixture.CreateDbContext();
        var result = await new AnalysisProcessingStateStore(dbContext)
            .TryStartAsync(request.Id);

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Request);
        Assert.Equal(status, (await ReadRequestAsync(request.Id)).Status);
    }

    [Fact]
    public async Task TryStartAsync_WhenResultAlreadyExists_ReturnsAlreadyCompleted()
    {
        var request = IntegrationTestData.CreateRequest(AnalysisStatus.Processing);
        var result = IntegrationTestData.CreateResult(request.Id);
        await SeedAsync(request, result);

        await using var dbContext = Fixture.CreateDbContext();
        var start = await new AnalysisProcessingStateStore(dbContext)
            .TryStartAsync(request.Id);

        Assert.Equal(AnalysisProcessingStartStatus.AlreadyCompleted, start.Status);
        Assert.Null(start.Request);
    }

    [Fact]
    public async Task TryStartAsync_WhenCalledConcurrently_OnlyOneCallerClaimsRequest()
    {
        var request = IntegrationTestData.CreateRequest(AnalysisStatus.Pending);
        await SeedAsync(request);

        await using var firstContext = Fixture.CreateDbContext();
        await using var secondContext = Fixture.CreateDbContext();
        var firstStore = new AnalysisProcessingStateStore(firstContext);
        var secondStore = new AnalysisProcessingStateStore(secondContext);

        var results = await Task.WhenAll(
            firstStore.TryStartAsync(request.Id),
            secondStore.TryStartAsync(request.Id));

        Assert.Single(
            results,
            result => result.Status == AnalysisProcessingStartStatus.Started);
        Assert.Single(
            results,
            result => result.Status == AnalysisProcessingStartStatus.AlreadyProcessing);
        Assert.Equal(
            AnalysisStatus.Processing,
            (await ReadRequestAsync(request.Id)).Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenResultBelongsToAnotherRequest_ThrowsBeforeWriting()
    {
        var request = IntegrationTestData.CreateRequest(AnalysisStatus.Processing);
        await SeedAsync(request);
        var result = IntegrationTestData.CreateResult(Guid.NewGuid());

        await using var dbContext = Fixture.CreateDbContext();
        var store = new AnalysisProcessingStateStore(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.CompleteAsync(request.Id, result, DateTime.UtcNow));

        Assert.Equal(AnalysisStatus.Processing, (await ReadRequestAsync(request.Id)).Status);
        Assert.Equal(0, await CountResultsAsync(request.Id));
    }

    [Fact]
    public async Task CompleteAsync_WhenRequestDoesNotExist_ReturnsNotFoundWithoutWriting()
    {
        var requestId = Guid.NewGuid();
        var result = IntegrationTestData.CreateResult(requestId);

        await using var dbContext = Fixture.CreateDbContext();
        var completion = await new AnalysisProcessingStateStore(dbContext)
            .CompleteAsync(requestId, result, DateTime.UtcNow);

        Assert.Equal(AnalysisCompletionStatus.NotFound, completion);
        Assert.Equal(0, await CountResultsAsync(requestId));
    }

    [Fact]
    public async Task CompleteAsync_PersistsEntireGraphAndCompletedStateAtomically()
    {
        var request = IntegrationTestData.CreateRequest(AnalysisStatus.Processing);
        var collectedItem = IntegrationTestData.CreateCollectedItem(request.Id);
        var completedAt = new DateTime(2026, 8, 21, 11, 0, 0, DateTimeKind.Utc);
        await SeedAsync(request, collectedItem);
        var result = IntegrationTestData.CreateResult(request.Id, collectedItem.Id);

        await using (var dbContext = Fixture.CreateDbContext())
        {
            var completion = await new AnalysisProcessingStateStore(dbContext)
                .CompleteAsync(request.Id, result, completedAt);

            Assert.Equal(AnalysisCompletionStatus.Completed, completion);
        }

        await using var readContext = Fixture.CreateDbContext();
        var persistedRequest = await readContext.AnalysisRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == request.Id);
        var persistedResult = await readContext.AnalysisResults
            .AsNoTracking()
            .Include(x => x.Insights)
                .ThenInclude(x => x.Evidence)
            .SingleAsync(x => x.AnalysisRequestId == request.Id);

        Assert.Equal(AnalysisStatus.Completed, persistedRequest.Status);
        Assert.Equal(completedAt, persistedRequest.CompletedAt);
        var insight = Assert.Single(persistedResult.Insights);
        var evidence = Assert.Single(insight.Evidence);
        Assert.Equal(collectedItem.Id, evidence.CollectedMarketItemId);
    }

    [Fact]
    public async Task CompleteAsync_WhenResultAlreadyExists_ReturnsAlreadyCompletedWithoutDuplicate()
    {
        var originalCompletedAt = new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
        var request = IntegrationTestData.CreateRequest(
            AnalysisStatus.Processing,
            originalCompletedAt);
        var existingResult = IntegrationTestData.CreateResult(request.Id, summary: "Existing");
        await SeedAsync(request, existingResult);
        var duplicate = IntegrationTestData.CreateResult(request.Id, summary: "Duplicate");

        await using (var dbContext = Fixture.CreateDbContext())
        {
            var completion = await new AnalysisProcessingStateStore(dbContext)
                .CompleteAsync(request.Id, duplicate, originalCompletedAt.AddHours(1));

            Assert.Equal(AnalysisCompletionStatus.AlreadyCompleted, completion);
        }

        var persisted = await ReadRequestAsync(request.Id);
        Assert.Equal(AnalysisStatus.Completed, persisted.Status);
        Assert.Equal(originalCompletedAt, persisted.CompletedAt);
        Assert.Equal(1, await CountResultsAsync(request.Id));
    }

    [Fact]
    public async Task CompleteAsync_WhenEvidenceBelongsToAnotherRequest_RollsBackEverything()
    {
        var request = IntegrationTestData.CreateRequest(AnalysisStatus.Processing);
        var otherRequest = IntegrationTestData.CreateRequest();
        var foreignItem = IntegrationTestData.CreateCollectedItem(otherRequest.Id);
        await SeedAsync(request, otherRequest, foreignItem);
        var result = IntegrationTestData.CreateResult(request.Id, foreignItem.Id);

        await using (var dbContext = Fixture.CreateDbContext())
        {
            var store = new AnalysisProcessingStateStore(dbContext);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.CompleteAsync(request.Id, result, DateTime.UtcNow));
        }

        Assert.Equal(AnalysisStatus.Processing, (await ReadRequestAsync(request.Id)).Status);
        Assert.Equal(0, await CountResultsAsync(request.Id));
    }

    [Fact]
    public async Task CompleteAsync_WhenPersistenceFails_RollsBackResultAndCompletedState()
    {
        var request = IntegrationTestData.CreateRequest(AnalysisStatus.Processing);
        await SeedAsync(request);
        var result = IntegrationTestData.CreateResult(request.Id);
        var firstInsight = CreateInsight(result.Id, 0);
        var duplicatePositionInsight = CreateInsight(result.Id, 0);
        result.Insights.Add(firstInsight);
        result.Insights.Add(duplicatePositionInsight);

        await using (var dbContext = Fixture.CreateDbContext())
        {
            var store = new AnalysisProcessingStateStore(dbContext);
            await Assert.ThrowsAsync<DbUpdateException>(
                () => store.CompleteAsync(request.Id, result, DateTime.UtcNow));
        }

        Assert.Equal(AnalysisStatus.Processing, (await ReadRequestAsync(request.Id)).Status);
        Assert.Equal(0, await CountResultsAsync(request.Id));
    }

    [Theory]
    [InlineData(AnalysisStatus.Pending)]
    [InlineData(AnalysisStatus.Processing)]
    public async Task MarkFailedAsync_WhenRequestCanFail_PersistsFailedState(
        AnalysisStatus initialStatus)
    {
        var request = IntegrationTestData.CreateRequest(initialStatus);
        var failedAt = new DateTime(2026, 8, 21, 13, 0, 0, DateTimeKind.Utc);
        await SeedAsync(request);

        await using (var dbContext = Fixture.CreateDbContext())
        {
            await new AnalysisProcessingStateStore(dbContext)
                .MarkFailedAsync(request.Id, failedAt);
        }

        var persisted = await ReadRequestAsync(request.Id);
        Assert.Equal(AnalysisStatus.Failed, persisted.Status);
        Assert.Equal(failedAt, persisted.CompletedAt);
    }

    [Fact]
    public async Task MarkFailedAsync_WhenRequestIsCompleted_DoesNotChangeIt()
    {
        var completedAt = new DateTime(2026, 8, 21, 14, 0, 0, DateTimeKind.Utc);
        var request = IntegrationTestData.CreateRequest(
            AnalysisStatus.Completed,
            completedAt);
        await SeedAsync(request);

        await using (var dbContext = Fixture.CreateDbContext())
        {
            await new AnalysisProcessingStateStore(dbContext)
                .MarkFailedAsync(request.Id, completedAt.AddHours(1));
        }

        var persisted = await ReadRequestAsync(request.Id);
        Assert.Equal(AnalysisStatus.Completed, persisted.Status);
        Assert.Equal(completedAt, persisted.CompletedAt);
    }

    [Fact]
    public async Task MarkFailedAsync_WhenResultExists_DoesNotChangeRequest()
    {
        var request = IntegrationTestData.CreateRequest(AnalysisStatus.Processing);
        var result = IntegrationTestData.CreateResult(request.Id);
        await SeedAsync(request, result);

        await using (var dbContext = Fixture.CreateDbContext())
        {
            await new AnalysisProcessingStateStore(dbContext)
                .MarkFailedAsync(request.Id, DateTime.UtcNow);
        }

        Assert.Equal(AnalysisStatus.Processing, (await ReadRequestAsync(request.Id)).Status);
    }

    [Fact]
    public async Task MarkFailedAsync_WhenRequestDoesNotExist_CompletesWithoutWriting()
    {
        await using var dbContext = Fixture.CreateDbContext();

        await new AnalysisProcessingStateStore(dbContext)
            .MarkFailedAsync(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(0, await dbContext.AnalysisRequests.CountAsync());
    }

    private async Task SeedAsync(params object[] entities)
    {
        await using var dbContext = Fixture.CreateDbContext();
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync();
    }

    private async Task<AnalysisRequest> ReadRequestAsync(Guid requestId)
    {
        await using var dbContext = Fixture.CreateDbContext();
        return await dbContext.AnalysisRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == requestId);
    }

    private async Task<int> CountResultsAsync(Guid requestId)
    {
        await using var dbContext = Fixture.CreateDbContext();
        return await dbContext.AnalysisResults
            .CountAsync(x => x.AnalysisRequestId == requestId);
    }

    private static AnalysisInsight CreateInsight(Guid resultId, int position)
        => new()
        {
            Id = Guid.NewGuid(),
            AnalysisResultId = resultId,
            Type = InsightType.Strength,
            Position = position,
            Text = $"Strength at {position}"
        };
}
