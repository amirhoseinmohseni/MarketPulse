using MarketPulse.Application.Services.DataCollection;
using MarketPulse.Application.Services.HackerNewsDataCollection;
using MarketPulse.Domain.Entities;
using MarketPulse.Infrastructure.HackerNews;

namespace MarketPulse.UnitTests.Infrastructure;

public class HackerNewsDataCollectorTests
{
    [Fact]
    public async Task CollectAsync_WithoutQueries_ReturnsSuccessWithoutDependencies()
    {
        var client = new StubHackerNewsClient();
        var repository = new RecordingCollectedMarketItemRepository();
        var collector = CreateCollector(client, repository);

        var result = await collector.CollectAsync(Context([]));

        Assert.True(result.Succeeded);
        Assert.Equal("HackerNews", result.SourceName);
        Assert.Equal(0, result.ItemsCollected);
        Assert.Empty(client.Requests);
        Assert.Empty(repository.ExistsCalls);
        Assert.Equal(0, repository.AddCalls);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task CollectAsync_SearchesEveryQueryAndMapsAllFieldsInOneSave()
    {
        var requestId = Guid.NewGuid();
        var queries = new[] { Query(requestId, "first"), Query(requestId, "second") };
        var created = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
        var client = new StubHackerNewsClient
        {
            Results = request =>
            [
                new HackerNewsSearchResult
                {
                    ExternalId = $"id-{request.Query}",
                    Title = "Title",
                    Content = "Content",
                    Url = "https://item.test",
                    Permalink = "https://item.test/permalink",
                    Score = 9,
                    CommentCount = 4,
                    CreatedUtc = created
                }
            ]
        };
        var repository = new RecordingCollectedMarketItemRepository();
        using var source = new CancellationTokenSource();
        var collector = CreateCollector(client, repository, defaultLimit: 13);

        var result = await collector.CollectAsync(Context(queries), source.Token);

        Assert.Equal(2, result.ItemsCollected);
        Assert.Equal(new[] { "first", "second" }, client.Requests.Select(x => x.Query));
        Assert.All(client.Requests, request => Assert.Equal(13, request.Limit));
        Assert.Equal(1, repository.AddCalls);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(source.Token, repository.AddToken);
        Assert.Equal(source.Token, repository.SaveToken);
        Assert.Equal(2, repository.Added.Count);
        Assert.All(repository.Added, item =>
        {
            Assert.Equal(requestId, item.AnalysisRequestId);
            Assert.Equal("HackerNews", item.Source);
            Assert.Equal("Title", item.Title);
            Assert.Equal("Content", item.Content);
            Assert.Equal("https://item.test", item.Url);
            Assert.Equal("https://item.test/permalink", item.Permalink);
            Assert.Equal(9, item.Score);
            Assert.Equal(4, item.CommentCount);
            Assert.Equal(created, item.CreatedUtc);
            Assert.Contains(item.SearchQueryId, queries.Select(query => (Guid?)query.Id));
        });
        Assert.All(repository.ExistsCalls, call =>
        {
            Assert.Equal(requestId, call.RequestId);
            Assert.Equal("HackerNews", call.Source);
        });
    }

    [Fact]
    public async Task CollectAsync_FiltersEmptyDuplicatesAndExistingItems()
    {
        var requestId = Guid.NewGuid();
        var queries = new[] { Query(requestId, "first"), Query(requestId, "second") };
        var client = new StubHackerNewsClient
        {
            Results = request => request.Query == "first"
                ? [Result(""), Result("Duplicate"), Result("existing")]
                : [Result("duplicate"), Result("new")]
        };
        var repository = new RecordingCollectedMarketItemRepository
        {
            Exists = (id, source, externalId) => externalId == "existing"
        };
        var collector = CreateCollector(client, repository);

        var result = await collector.CollectAsync(Context(queries));

        Assert.Equal(2, result.ItemsCollected);
        Assert.Equal(new[] { "Duplicate", "new" }, repository.Added.Select(x => x.ExternalId));
        Assert.Equal(1, repository.AddCalls);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task CollectAsync_WhenNoNewItems_DoesNotSave()
    {
        var requestId = Guid.NewGuid();
        var client = new StubHackerNewsClient { Results = _ => [Result("existing")] };
        var repository = new RecordingCollectedMarketItemRepository { Exists = (_, _, _) => true };
        var collector = CreateCollector(client, repository);

        var result = await collector.CollectAsync(Context([Query(requestId, "one")]));

        Assert.Equal(0, result.ItemsCollected);
        Assert.Equal(0, repository.AddCalls);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task CollectAsync_WhenClientFails_PropagatesWithoutPartialSave()
    {
        var requestId = Guid.NewGuid();
        var queries = new[] { Query(requestId, "first"), Query(requestId, "fail") };
        var client = new StubHackerNewsClient
        {
            Results = request => request.Query == "fail"
                ? throw new InvalidOperationException("client failed")
                : [Result("partial")]
        };
        var repository = new RecordingCollectedMarketItemRepository();
        var collector = CreateCollector(client, repository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => collector.CollectAsync(Context(queries)));

        Assert.Equal(0, repository.AddCalls);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task CollectAsync_WhenCallerCancels_PropagatesWithoutSaving()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var requestId = Guid.NewGuid();
        var client = new StubHackerNewsClient
        {
            Handler = (_, token) => Task.FromCanceled<IReadOnlyList<HackerNewsSearchResult>>(token)
        };
        var repository = new RecordingCollectedMarketItemRepository();
        var collector = CreateCollector(client, repository);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => collector.CollectAsync(Context([Query(requestId, "one")]), source.Token));
        Assert.Equal(0, repository.SaveCalls);
    }

    private static HackerNewsDataCollector CreateCollector(
        StubHackerNewsClient client,
        RecordingCollectedMarketItemRepository repository,
        int defaultLimit = 20)
        => new(client, repository, new HackerNewsOptions { DefaultLimit = defaultLimit });

    private static DataCollectionContext Context(IReadOnlyCollection<SearchQuery> queries)
        => new()
        {
            AnalysisRequestId = queries.FirstOrDefault()?.AnalysisRequestId ?? Guid.NewGuid(),
            Idea = "idea",
            SearchQueries = queries
        };

    private static SearchQuery Query(Guid requestId, string query)
        => new() { Id = Guid.NewGuid(), AnalysisRequestId = requestId, Query = query, Priority = 1 };

    private static HackerNewsSearchResult Result(string id)
        => new() { ExternalId = id, Title = id };

    private sealed class StubHackerNewsClient : IHackerNewsClient
    {
        public List<HackerNewsSearchRequest> Requests { get; } = [];
        public Func<HackerNewsSearchRequest, IReadOnlyList<HackerNewsSearchResult>> Results { get; init; }
            = _ => [];
        public Func<HackerNewsSearchRequest, CancellationToken, Task<IReadOnlyList<HackerNewsSearchResult>>>? Handler { get; init; }

        public Task<IReadOnlyList<HackerNewsSearchResult>> SearchAsync(
            HackerNewsSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Handler?.Invoke(request, cancellationToken)
                ?? Task.FromResult(Results(request));
        }
    }
}
