using MarketPulse.Application.Services.DataCollection;
using MarketPulse.Application.Services.RedditDataCollection;
using MarketPulse.Domain.Entities;
using MarketPulse.Infrastructure.Reddit;

namespace MarketPulse.UnitTests.Infrastructure;

public class RedditDataCollectorTests
{
    [Fact]
    public async Task CollectAsync_WithoutQueries_ReturnsSuccessWithoutDependencies()
    {
        var client = new StubRedditClient();
        var repository = new RecordingCollectedMarketItemRepository();
        var collector = CreateCollector(client, repository);

        var result = await collector.CollectAsync(Context([]));

        Assert.True(result.Succeeded);
        Assert.Equal("Reddit", result.SourceName);
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
        var client = new StubRedditClient
        {
            Results = request =>
            [
                new RedditPostSearchResult
                {
                    RedditPostId = $"id-{request.Query}",
                    Subreddit = "startups",
                    Title = "Title",
                    SelfText = "Content",
                    Url = "https://reddit.test/item",
                    Permalink = "/r/startups/item",
                    Score = 9,
                    CommentCount = 4,
                    CreatedUtc = created
                }
            ]
        };
        var repository = new RecordingCollectedMarketItemRepository();
        using var source = new CancellationTokenSource();
        var collector = CreateCollector(client, repository, defaultLimit: 17);

        var result = await collector.CollectAsync(Context(queries), source.Token);

        Assert.Equal(2, result.ItemsCollected);
        Assert.Equal(new[] { "first", "second" }, client.Requests.Select(x => x.Query));
        Assert.All(client.Requests, request => Assert.Equal(17, request.Limit));
        Assert.Equal(1, repository.AddCalls);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(2, repository.Added.Count);
        Assert.All(repository.Added, item =>
        {
            Assert.Equal(requestId, item.AnalysisRequestId);
            Assert.Equal("Reddit", item.Source);
            Assert.Equal("Title", item.Title);
            Assert.Equal("Content", item.Content);
            Assert.Equal("https://reddit.test/item", item.Url);
            Assert.Equal("/r/startups/item", item.Permalink);
            Assert.Equal(9, item.Score);
            Assert.Equal(4, item.CommentCount);
            Assert.Equal(created, item.CreatedUtc);
            Assert.Contains(item.SearchQueryId, queries.Select(query => (Guid?)query.Id));
        });
        Assert.All(repository.ExistsCalls, call =>
        {
            Assert.Equal(requestId, call.RequestId);
            Assert.Equal("Reddit", call.Source);
        });
    }

    [Fact]
    public async Task CollectAsync_FiltersEmptyDuplicatesAndExistingItems()
    {
        var requestId = Guid.NewGuid();
        var queries = new[] { Query(requestId, "first"), Query(requestId, "second") };
        var client = new StubRedditClient
        {
            Results = request => request.Query == "first"
                ? [Result(""), Result("Duplicate"), Result("existing")]
                : [Result("duplicate"), Result("new")]
        };
        var repository = new RecordingCollectedMarketItemRepository
        {
            Exists = (_, _, externalId) => externalId == "existing"
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
        var client = new StubRedditClient { Results = _ => [Result("existing")] };
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
        var client = new StubRedditClient
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
        var client = new StubRedditClient
        {
            Handler = (_, token) => Task.FromCanceled<IReadOnlyList<RedditPostSearchResult>>(token)
        };
        var repository = new RecordingCollectedMarketItemRepository();
        var collector = CreateCollector(client, repository);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => collector.CollectAsync(Context([Query(requestId, "one")]), source.Token));
        Assert.Equal(0, repository.SaveCalls);
    }

    private static RedditDataCollector CreateCollector(
        StubRedditClient client,
        RecordingCollectedMarketItemRepository repository,
        int defaultLimit = 25)
        => new(client, repository, new RedditOptions { DefaultLimit = defaultLimit });

    private static DataCollectionContext Context(IReadOnlyCollection<SearchQuery> queries)
        => new()
        {
            AnalysisRequestId = queries.FirstOrDefault()?.AnalysisRequestId ?? Guid.NewGuid(),
            Idea = "idea",
            SearchQueries = queries
        };

    private static SearchQuery Query(Guid requestId, string query)
        => new() { Id = Guid.NewGuid(), AnalysisRequestId = requestId, Query = query, Priority = 1 };

    private static RedditPostSearchResult Result(string id)
        => new() { RedditPostId = id, Title = id };

    private sealed class StubRedditClient : IRedditClient
    {
        public List<RedditPostSearchRequest> Requests { get; } = [];
        public Func<RedditPostSearchRequest, IReadOnlyList<RedditPostSearchResult>> Results { get; init; }
            = _ => [];
        public Func<RedditPostSearchRequest, CancellationToken, Task<IReadOnlyList<RedditPostSearchResult>>>? Handler { get; init; }

        public Task<IReadOnlyList<RedditPostSearchResult>> SearchPostsAsync(
            RedditPostSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Handler?.Invoke(request, cancellationToken)
                ?? Task.FromResult(Results(request));
        }
    }
}
