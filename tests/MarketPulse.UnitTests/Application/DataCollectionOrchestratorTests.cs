using MarketPulse.Application.Services.DataCollection;
using MarketPulse.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MarketPulse.UnitTests.Application;

public class DataCollectionOrchestratorTests
{
    [Fact]
    public async Task CollectAsync_WhenNoCollectorsAreEnabled_ReturnsEmpty()
    {
        var orchestrator = CreateOrchestrator([], failFast: false);

        var result = await orchestrator.CollectAsync(Guid.NewGuid(), "idea", []);

        Assert.Empty(result);
    }

    [Fact]
    public async Task CollectAsync_ExecutesInOrderAndPassesExactContextAndResults()
    {
        var calls = new List<string>();
        var firstResult = Success("First", 2);
        var secondResult = Success("Second", 3);
        var first = new StubCollector("First", (context, _) =>
        {
            calls.Add("First");
            return Task.FromResult(firstResult);
        });
        var second = new StubCollector("Second", (context, _) =>
        {
            calls.Add("Second");
            return Task.FromResult(secondResult);
        });
        var requestId = Guid.NewGuid();
        var queries = new[] { CreateQuery(requestId) };
        var orchestrator = CreateOrchestrator([first, second], failFast: false);

        var results = await orchestrator.CollectAsync(requestId, "exact idea", queries);

        Assert.Equal(new[] { "First", "Second" }, calls);
        Assert.Same(firstResult, results[0]);
        Assert.Same(secondResult, results[1]);
        Assert.All(new[] { first, second }, collector =>
        {
            Assert.Equal(requestId, collector.Context!.AnalysisRequestId);
            Assert.Equal("exact idea", collector.Context.Idea);
            Assert.Same(queries, collector.Context.SearchQueries);
        });
    }

    [Fact]
    public async Task CollectAsync_WhenFailFastIsFalse_ReturnsSafeFailureAndContinues()
    {
        const string sensitiveMarker = "SENSITIVE-COLLECTOR-CONTENT";
        var logger = new RecordingLogger<DataCollectionOrchestrator>();
        var failing = new StubCollector(
            "Broken",
            (_, _) => throw new InvalidOperationException(sensitiveMarker));
        var following = new StubCollector(
            "Following",
            (_, _) => Task.FromResult(Success("Following", 1)));
        var orchestrator = CreateOrchestrator(
            [failing, following],
            failFast: false,
            logger);

        var results = await orchestrator.CollectAsync(Guid.NewGuid(), "idea", []);

        Assert.Equal(2, results.Count);
        Assert.Equal("Broken", results[0].SourceName);
        Assert.False(results[0].Succeeded);
        Assert.Equal(0, results[0].ItemsCollected);
        Assert.Equal("Data collector failed.", results[0].ErrorMessage);
        Assert.True(results[1].Succeeded);
        Assert.DoesNotContain(
            sensitiveMarker,
            string.Join(" ", logger.Messages),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CollectAsync_WhenFailFastIsTrue_RethrowsOriginalFailure()
    {
        var expected = new InvalidOperationException("failure");
        var collector = new StubCollector("Broken", (_, _) => throw expected);
        var orchestrator = CreateOrchestrator([collector], failFast: true);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.CollectAsync(Guid.NewGuid(), "idea", []));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task CollectAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var collector = new StubCollector(
            "Cancelled",
            (_, token) => Task.FromCanceled<DataCollectionResult>(token));
        var orchestrator = CreateOrchestrator([collector], failFast: false);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => orchestrator.CollectAsync(
                Guid.NewGuid(),
                "idea",
                [],
                cancellationSource.Token));
    }

    [Fact]
    public async Task CollectAsync_WhenCancellationIsNotFromCaller_AppliesFailurePolicy()
    {
        var collector = new StubCollector(
            "CancelledInternally",
            (_, _) => throw new OperationCanceledException("internal"));
        var orchestrator = CreateOrchestrator([collector], failFast: false);

        var result = Assert.Single(
            await orchestrator.CollectAsync(Guid.NewGuid(), "idea", []));

        Assert.False(result.Succeeded);
        Assert.Equal("Data collector failed.", result.ErrorMessage);
    }

    private static DataCollectionOrchestrator CreateOrchestrator(
        IReadOnlyList<IDataCollector> collectors,
        bool failFast,
        ILogger<DataCollectionOrchestrator>? logger = null)
        => new(
            new StubFactory(collectors),
            new DataCollectorOptions { FailFast = failFast },
            logger ?? new RecordingLogger<DataCollectionOrchestrator>());

    private static DataCollectionResult Success(string source, int count)
        => new()
        {
            SourceName = source,
            ItemsCollected = count,
            Succeeded = true
        };

    private static SearchQuery CreateQuery(Guid requestId)
        => new()
        {
            Id = Guid.NewGuid(),
            AnalysisRequestId = requestId,
            Query = "test query",
            Priority = 1
        };

    private sealed class StubFactory(IReadOnlyList<IDataCollector> collectors)
        : IDataCollectorFactory
    {
        public IReadOnlyList<IDataCollector> GetEnabledCollectors() => collectors;
    }

    private sealed class StubCollector(
        string sourceName,
        Func<DataCollectionContext, CancellationToken, Task<DataCollectionResult>> collect)
        : IDataCollector
    {
        public string SourceName => sourceName;
        public DataCollectionContext? Context { get; private set; }

        public Task<DataCollectionResult> CollectAsync(
            DataCollectionContext context,
            CancellationToken cancellationToken = default)
        {
            Context = context;
            return collect(context, cancellationToken);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
