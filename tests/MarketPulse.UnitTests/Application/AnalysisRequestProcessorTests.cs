using System.Text.Json;
using MarketPulse.Application.Services.Analyser;
using MarketPulse.Application.Services.AnalysisProcessing;
using MarketPulse.Application.Services.DataCollection;
using MarketPulse.Application.Services.SearchQueryGenerator;
using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Enums;
using MarketPulse.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketPulse.UnitTests.Application;

public class AnalysisRequestProcessorTests
{
    [Fact]
    public async Task HappyPath_ProcessesCollectedItemsAndCompletesWithRealEvidenceId()
    {
        var requestId = Guid.NewGuid();
        var item = CreateItem(requestId, "Repeated customer demand with meaningful engagement.");
        var harness = CreateHarness(
            requestId,
            [item],
            ValidResponse("C001"),
            CreateOptions(moderateMinItems: 1, moderateMinSources: 1));

        var outcome = await harness.Processor.ProcessAsync(requestId);

        Assert.Equal(AnalysisProcessingOutcome.Completed, outcome);
        Assert.Equal(AnalysisStatus.Completed, harness.StateStore.Status);
        Assert.NotNull(harness.StateStore.Result);
        Assert.Equal(1, harness.StateStore.CompletionCount);
        Assert.Equal(1, harness.Collection.CallCount);
        Assert.Equal(1, harness.AiClient.CallCount);
        var evidence = Assert.Single(
            Assert.Single(harness.StateStore.Result.Insights).Evidence);
        Assert.Equal(item.Id, evidence.CollectedMarketItemId);
    }

    [Fact]
    public async Task NoData_CompletesWeakWithoutCallingProvider()
    {
        var requestId = Guid.NewGuid();
        var harness = CreateHarness(requestId, [], ValidResponse("C001"));

        var outcome = await harness.Processor.ProcessAsync(requestId);

        Assert.Equal(AnalysisProcessingOutcome.Completed, outcome);
        Assert.Equal(AnalysisStatus.Completed, harness.StateStore.Status);
        Assert.Equal(SignalStrength.Weak, harness.StateStore.Result?.SignalStrength);
        Assert.Null(harness.StateStore.Result?.MarketScore);
        Assert.Empty(harness.StateStore.Result!.Insights);
        Assert.Equal(0, harness.AiClient.CallCount);
    }

    [Fact]
    public async Task WeakInput_CannotBeUpgradedByProvider()
    {
        var requestId = Guid.NewGuid();
        var item = CreateItem(requestId, "One isolated market discussion with usable content.");
        var harness = CreateHarness(
            requestId,
            [item],
            ValidResponse("C001", marketScore: 95, signalStrength: "Strong"));

        var outcome = await harness.Processor.ProcessAsync(requestId);

        Assert.Equal(AnalysisProcessingOutcome.Completed, outcome);
        Assert.Equal(SignalStrength.Weak, harness.StateStore.Result?.SignalStrength);
        Assert.Null(harness.StateStore.Result?.MarketScore);
        Assert.Contains("limited in volume", harness.StateStore.Result?.Summary);
    }

    [Fact]
    public async Task InvalidEvidence_FailsRequestWithoutSavingResult()
    {
        var requestId = Guid.NewGuid();
        var item = CreateItem(requestId, "A useful collected discussion for validation.");
        var harness = CreateHarness(requestId, [item], ValidResponse("C999"));

        var outcome = await harness.Processor.ProcessAsync(requestId);

        Assert.Equal(AnalysisProcessingOutcome.Failed, outcome);
        Assert.Equal(AnalysisStatus.Failed, harness.StateStore.Status);
        Assert.Null(harness.StateStore.Result);
        Assert.Equal(1, harness.StateStore.MarkFailedCount);
    }

    [Fact]
    public async Task ProviderFailure_TransitionsRequestToFailed()
    {
        var requestId = Guid.NewGuid();
        var item = CreateItem(requestId, "A useful collected discussion for validation.");
        var harness = CreateHarness(
            requestId,
            [item],
            aiHandler: (_, _) => throw new HttpRequestException("Provider unavailable."));

        var outcome = await harness.Processor.ProcessAsync(requestId);

        Assert.Equal(AnalysisProcessingOutcome.Failed, outcome);
        Assert.Equal(AnalysisStatus.Failed, harness.StateStore.Status);
        Assert.Null(harness.StateStore.Result);
        Assert.NotNull(harness.StateStore.CompletedAt);
    }

    [Fact]
    public async Task PersistenceFailure_TransitionsRequestToFailedWithoutRetainingResult()
    {
        var requestId = Guid.NewGuid();
        var item = CreateItem(requestId, "A useful collected discussion for validation.");
        var harness = CreateHarness(requestId, [item], ValidResponse("C001"));
        harness.StateStore.CompletionException = new InvalidOperationException(
            "Simulated persistence failure.");

        var outcome = await harness.Processor.ProcessAsync(requestId);

        Assert.Equal(AnalysisProcessingOutcome.Failed, outcome);
        Assert.Equal(AnalysisStatus.Failed, harness.StateStore.Status);
        Assert.Null(harness.StateStore.Result);
        Assert.Equal(1, harness.StateStore.MarkFailedCount);
    }

    [Fact]
    public async Task ShutdownCancellation_DoesNotMarkRequestFailed()
    {
        var requestId = Guid.NewGuid();
        var item = CreateItem(requestId, "A useful collected discussion for validation.");
        var harness = CreateHarness(
            requestId,
            [item],
            aiHandler: async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return string.Empty;
            });
        using var cancellationSource = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Processor.ProcessAsync(
                requestId,
                cancellationSource.Token));

        Assert.Equal(AnalysisStatus.Processing, harness.StateStore.Status);
        Assert.Equal(0, harness.StateStore.MarkFailedCount);
        Assert.Null(harness.StateStore.Result);
    }

    [Fact]
    public async Task DuplicateDelivery_DoesNotCreateASecondResultOrProviderCall()
    {
        var requestId = Guid.NewGuid();
        var item = CreateItem(requestId, "A useful collected discussion for validation.");
        var harness = CreateHarness(requestId, [item], ValidResponse("C001"));

        var first = await harness.Processor.ProcessAsync(requestId);
        var second = await harness.Processor.ProcessAsync(requestId);

        Assert.Equal(AnalysisProcessingOutcome.Completed, first);
        Assert.Equal(AnalysisProcessingOutcome.SkippedCompleted, second);
        Assert.Equal(1, harness.StateStore.CompletionCount);
        Assert.Equal(1, harness.AiClient.CallCount);
        Assert.Equal(1, harness.Collection.CallCount);
    }

    [Fact]
    public async Task ItemOwnedByAnotherRequest_IsExcludedFromPromptAndEvidence()
    {
        var requestId = Guid.NewGuid();
        var owned = CreateItem(requestId, "Owned collected market discussion.");
        var foreign = CreateItem(
            Guid.NewGuid(),
            "FOREIGN-REQUEST-CONTENT must never be analyzed.");
        foreign.Score = 1_000;
        var harness = CreateHarness(
            requestId,
            [foreign, owned],
            ValidResponse("C001"));

        var outcome = await harness.Processor.ProcessAsync(requestId);

        Assert.Equal(AnalysisProcessingOutcome.Completed, outcome);
        Assert.DoesNotContain(
            "FOREIGN-REQUEST-CONTENT",
            harness.AiClient.LastRequest?.UserPrompt);
        var evidence = Assert.Single(
            Assert.Single(harness.StateStore.Result!.Insights).Evidence);
        Assert.Equal(owned.Id, evidence.CollectedMarketItemId);
    }

    private static ProcessorHarness CreateHarness(
        Guid requestId,
        IReadOnlyList<CollectedMarketItem> items,
        string? response = null,
        MarketInsightAnalysisOptions? options = null,
        Func<AiMarketInsightRequest, CancellationToken, Task<string>>? aiHandler = null)
    {
        options ??= CreateOptions();
        var stateStore = new InMemoryProcessingStateStore(
            requestId,
            "A test product idea");
        var aiClient = new FakeAiMarketInsightClient(
            aiHandler ?? ((_, _) => Task.FromResult(response ?? ValidResponse("C001"))));
        var collectedRepository = new StubCollectedMarketItemRepository(items);
        var generator = new AnalysisGenerator(
            collectedRepository,
            aiClient,
            new MarketInsightInputBuilder(options),
            new MarketInsightSignalEvaluator(options),
            new MarketInsightPromptBuilder(options),
            new MarketInsightResponseParser(options));
        var collection = new StubDataCollectionOrchestrator();
        var processor = new AnalysisRequestProcessor(
            stateStore,
            new StubSearchQueryGenerationService(),
            new StubSearchQueryRepository(requestId),
            collection,
            generator,
            new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<AnalysisRequestProcessor>.Instance);

        return new ProcessorHarness(
            processor,
            stateStore,
            aiClient,
            collection);
    }

    private static MarketInsightAnalysisOptions CreateOptions(
        int moderateMinItems = 4,
        int moderateMinSources = 2)
        => new()
        {
            MinUsableTextLength = 20,
            ModerateMinItems = moderateMinItems,
            ModerateMinSources = moderateMinSources
        };

    private static CollectedMarketItem CreateItem(Guid requestId, string content)
        => new()
        {
            Id = Guid.NewGuid(),
            AnalysisRequestId = requestId,
            Source = "HackerNews",
            ExternalId = Guid.NewGuid().ToString("N"),
            Title = "Collected item",
            Content = content,
            CollectedAt = DateTime.UtcNow
        };

    private static string ValidResponse(
        string evidenceId,
        int? marketScore = 60,
        string signalStrength = "Moderate")
        => JsonSerializer.Serialize(new
        {
            marketScore,
            signalStrength,
            summary = "A grounded market summary.",
            strengths = new[]
            {
                new
                {
                    text = "A grounded strength.",
                    evidenceIds = new[] { evidenceId }
                }
            },
            weaknesses = Array.Empty<object>(),
            opportunities = Array.Empty<object>(),
            risks = Array.Empty<object>()
        });

    private sealed record ProcessorHarness(
        AnalysisRequestProcessor Processor,
        InMemoryProcessingStateStore StateStore,
        FakeAiMarketInsightClient AiClient,
        StubDataCollectionOrchestrator Collection);

    private sealed class InMemoryProcessingStateStore(
        Guid requestId,
        string idea)
        : IAnalysisProcessingStateStore
    {
        public AnalysisStatus Status { get; private set; } = AnalysisStatus.Pending;
        public AnalysisResult? Result { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public int CompletionCount { get; private set; }
        public int MarkFailedCount { get; private set; }
        public Exception? CompletionException { get; set; }

        public Task<AnalysisProcessingStartResult> TryStartAsync(
            Guid analysisRequestId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (analysisRequestId != requestId)
            {
                return Task.FromResult(new AnalysisProcessingStartResult(
                    AnalysisProcessingStartStatus.NotFound));
            }

            if (Result is not null || Status == AnalysisStatus.Completed)
            {
                return Task.FromResult(new AnalysisProcessingStartResult(
                    AnalysisProcessingStartStatus.AlreadyCompleted));
            }

            if (Status == AnalysisStatus.Processing)
            {
                return Task.FromResult(new AnalysisProcessingStartResult(
                    AnalysisProcessingStartStatus.AlreadyProcessing));
            }

            Status = AnalysisStatus.Processing;
            CompletedAt = null;
            return Task.FromResult(new AnalysisProcessingStartResult(
                AnalysisProcessingStartStatus.Started,
                new AnalysisProcessingRequest(requestId, idea)));
        }

        public Task<AnalysisCompletionStatus> CompleteAsync(
            Guid analysisRequestId,
            AnalysisResult result,
            DateTime completedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CompletionException is not null)
            {
                throw CompletionException;
            }

            if (Result is not null)
            {
                return Task.FromResult(AnalysisCompletionStatus.AlreadyCompleted);
            }

            Result = result;
            Status = AnalysisStatus.Completed;
            CompletedAt = completedAtUtc;
            CompletionCount++;
            return Task.FromResult(AnalysisCompletionStatus.Completed);
        }

        public Task MarkFailedAsync(
            Guid analysisRequestId,
            DateTime failedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Status = AnalysisStatus.Failed;
            CompletedAt = failedAtUtc;
            MarkFailedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAiMarketInsightClient(
        Func<AiMarketInsightRequest, CancellationToken, Task<string>> handler)
        : IAiMarketInsightClient
    {
        public int CallCount { get; private set; }
        public AiMarketInsightRequest? LastRequest { get; private set; }

        public Task<string> GenerateAsync(
            AiMarketInsightRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return handler(request, cancellationToken);
        }
    }

    private sealed class StubCollectedMarketItemRepository(
        IReadOnlyList<CollectedMarketItem> items)
        : ICollectedMarketItemRepository
    {
        public Task<IReadOnlyList<CollectedMarketItem>> GetByAnalysisRequestIdAsync(
            Guid analysisRequestId,
            CancellationToken ct = default)
            => Task.FromResult(items);

        public Task AddRangeAsync(
            IEnumerable<CollectedMarketItem> collectedItems,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> ExistsForAnalysisRequestAsync(
            Guid analysisRequestId,
            string source,
            string externalId,
            CancellationToken ct = default)
            => Task.FromResult(false);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubSearchQueryRepository(Guid requestId)
        : ISearchQueryRepository
    {
        private readonly IReadOnlyList<SearchQuery> _queries =
        [
            new SearchQuery
            {
                Id = Guid.NewGuid(),
                AnalysisRequestId = requestId,
                Query = "market problem",
                Category = QueryCategory.Problem,
                Priority = 1
            }
        ];

        public Task<IReadOnlyList<SearchQuery>> GetByAnalysisRequestIdAsync(
            Guid analysisRequestId,
            CancellationToken ct = default)
            => Task.FromResult(_queries);

        public Task AddRangeAsync(
            IEnumerable<SearchQuery> searchQueries,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubSearchQueryGenerationService
        : ISearchQueryGenerationService
    {
        public Task GenerateForAnalysisRequestAsync(
            Guid analysisRequestId,
            string idea,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubDataCollectionOrchestrator
        : IDataCollectionOrchestrator
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<DataCollectionResult>> CollectAsync(
            Guid analysisRequestId,
            string idea,
            IReadOnlyCollection<SearchQuery> searchQueries,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult<IReadOnlyList<DataCollectionResult>>([]);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
