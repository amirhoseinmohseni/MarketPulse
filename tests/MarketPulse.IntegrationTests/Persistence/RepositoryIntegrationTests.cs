using MarketPulse.Domain.Enums;
using MarketPulse.Infrastructure.Repositories;
using MarketPulse.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MarketPulse.IntegrationTests.Persistence;

public sealed class RepositoryIntegrationTests(PostgreSqlIntegrationFixture fixture)
    : PostgreSqlIntegrationTestBase(fixture)
{
    [Fact]
    public async Task AnalysisRequestRepository_AddsAndReadsRequestById()
    {
        var request = IntegrationTestData.CreateRequest();

        await using (var writeContext = Fixture.CreateDbContext())
        {
            var repository = new AnalysisRequestRepository(writeContext);
            await repository.AddAsync(request);
            await repository.SaveChangesAsync();
        }

        await using var readContext = Fixture.CreateDbContext();
        var persisted = await new AnalysisRequestRepository(readContext)
            .GetByIdAsync(request.Id);

        Assert.NotNull(persisted);
        Assert.Equal(request.Id, persisted.Id);
        Assert.Equal(request.Idea, persisted.Idea);
        Assert.Equal(AnalysisStatus.Pending, persisted.Status);
    }

    [Fact]
    public async Task AnalysisRequestRepository_WhenRequestDoesNotExist_ReturnsNull()
    {
        await using var dbContext = Fixture.CreateDbContext();
        var repository = new AnalysisRequestRepository(dbContext);

        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid()));
        Assert.Null(await repository.GetByIdWithResultAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AnalysisRequestRepository_GetWithResult_LoadsEntireEvidenceGraph()
    {
        var request = IntegrationTestData.CreateRequest(AnalysisStatus.Completed);
        var query = IntegrationTestData.CreateSearchQuery(request.Id);
        var item = IntegrationTestData.CreateCollectedItem(request.Id, query.Id);
        var result = IntegrationTestData.CreateResult(request.Id, item.Id);
        await SeedAsync(request, query, item, result);

        await using var dbContext = Fixture.CreateDbContext();
        var loaded = await new AnalysisRequestRepository(dbContext)
            .GetByIdWithResultAsync(request.Id);

        Assert.NotNull(loaded?.Result);
        var insight = Assert.Single(loaded.Result.Insights);
        var evidence = Assert.Single(insight.Evidence);
        Assert.NotNull(evidence.CollectedMarketItem);
        Assert.Equal(item.Id, evidence.CollectedMarketItem.Id);
        Assert.Equal(item.Source, evidence.CollectedMarketItem.Source);
    }

    [Fact]
    public async Task SearchQueryRepository_ReturnsOnlyRequestedQueriesInHighestPriorityFirstOrder()
    {
        var request = IntegrationTestData.CreateRequest();
        var otherRequest = IntegrationTestData.CreateRequest();
        await SeedAsync(request, otherRequest);
        var expected = new[]
        {
            IntegrationTestData.CreateSearchQuery(request.Id, 3, "priority-three"),
            IntegrationTestData.CreateSearchQuery(request.Id, 1, "priority-one"),
            IntegrationTestData.CreateSearchQuery(request.Id, 2, "priority-two")
        };
        var unrelated = IntegrationTestData.CreateSearchQuery(
            otherRequest.Id,
            1,
            "unrelated");

        await using (var writeContext = Fixture.CreateDbContext())
        {
            var repository = new SearchQueryRepository(writeContext);
            await repository.AddRangeAsync(expected.Append(unrelated));
            await repository.SaveChangesAsync();
        }

        await using var readContext = Fixture.CreateDbContext();
        var actual = await new SearchQueryRepository(readContext)
            .GetByAnalysisRequestIdAsync(request.Id);

        Assert.Equal(
            new[] { "priority-one", "priority-two", "priority-three" },
            actual.Select(query => query.Query));
    }

    [Fact]
    public async Task SearchQueryRepository_ReturnsUntrackedEntities()
    {
        var request = IntegrationTestData.CreateRequest();
        var otherRequest = IntegrationTestData.CreateRequest();
        var query = IntegrationTestData.CreateSearchQuery(request.Id);
        await SeedAsync(request, otherRequest, query);

        await using (var readContext = Fixture.CreateDbContext())
        {
            var repository = new SearchQueryRepository(readContext);
            var loaded = Assert.Single(
                await repository.GetByAnalysisRequestIdAsync(request.Id));
            loaded.AnalysisRequestId = otherRequest.Id;
            await repository.SaveChangesAsync();
        }

        await using var verifyContext = Fixture.CreateDbContext();
        var persisted = await verifyContext.SearchQueries
            .AsNoTracking()
            .SingleAsync(x => x.Id == query.Id);
        Assert.Equal(request.Id, persisted.AnalysisRequestId);
    }

    [Fact]
    public async Task CollectedMarketItemRepository_ScopesReadsAndExistenceToRequestAndSource()
    {
        var request = IntegrationTestData.CreateRequest();
        var otherRequest = IntegrationTestData.CreateRequest();
        await SeedAsync(request, otherRequest);
        const string externalId = "shared-external-id";
        var hackerNewsItem = IntegrationTestData.CreateCollectedItem(
            request.Id,
            source: "HackerNews",
            externalId: externalId);
        var redditItem = IntegrationTestData.CreateCollectedItem(
            request.Id,
            source: "Reddit",
            externalId: externalId);
        var otherRequestItem = IntegrationTestData.CreateCollectedItem(
            otherRequest.Id,
            source: "HackerNews",
            externalId: externalId);

        await using (var writeContext = Fixture.CreateDbContext())
        {
            var repository = new CollectedMarketItemRepository(writeContext);
            await repository.AddRangeAsync(
                [hackerNewsItem, redditItem, otherRequestItem]);
            await repository.SaveChangesAsync();
        }

        await using var readContext = Fixture.CreateDbContext();
        var readRepository = new CollectedMarketItemRepository(readContext);
        var requestItems = await readRepository.GetByAnalysisRequestIdAsync(request.Id);

        Assert.Equal(2, requestItems.Count);
        Assert.DoesNotContain(
            requestItems,
            item => item.AnalysisRequestId == otherRequest.Id);
        Assert.True(await readRepository.ExistsForAnalysisRequestAsync(
            request.Id,
            "HackerNews",
            externalId));
        Assert.True(await readRepository.ExistsForAnalysisRequestAsync(
            request.Id,
            "Reddit",
            externalId));
        Assert.False(await readRepository.ExistsForAnalysisRequestAsync(
            request.Id,
            "Unknown",
            externalId));
    }

    [Fact]
    public async Task CollectedMarketItemDatabaseConstraint_AllowsSameExternalIdAcrossRequests()
    {
        var request = IntegrationTestData.CreateRequest();
        var otherRequest = IntegrationTestData.CreateRequest();
        const string externalId = "request-scoped-item";
        await SeedAsync(
            request,
            otherRequest,
            IntegrationTestData.CreateCollectedItem(
                request.Id,
                externalId: externalId),
            IntegrationTestData.CreateCollectedItem(
                otherRequest.Id,
                externalId: externalId));

        await using var dbContext = Fixture.CreateDbContext();
        Assert.Equal(
            2,
            await dbContext.CollectedMarketItems.CountAsync(
                item => item.ExternalId == externalId));
    }

    [Fact]
    public async Task RedditPostRepository_ScopesReadsAndExistenceToRequest()
    {
        var request = IntegrationTestData.CreateRequest();
        var otherRequest = IntegrationTestData.CreateRequest();
        await SeedAsync(request, otherRequest);
        const string redditPostId = "shared-reddit-id";
        var requestPost = IntegrationTestData.CreateRedditPost(
            request.Id,
            redditPostId: redditPostId);
        var otherPost = IntegrationTestData.CreateRedditPost(
            otherRequest.Id,
            redditPostId: redditPostId);

        await using (var writeContext = Fixture.CreateDbContext())
        {
            var repository = new RedditPostRepository(writeContext);
            await repository.AddRangeAsync([requestPost, otherPost]);
            await repository.SaveChangesAsync();
        }

        await using var readContext = Fixture.CreateDbContext();
        var readRepository = new RedditPostRepository(readContext);
        var posts = await readRepository.GetByAnalysisRequestIdAsync(request.Id);

        Assert.Equal(requestPost.Id, Assert.Single(posts).Id);
        Assert.True(await readRepository.ExistsForAnalysisRequestAsync(
            request.Id,
            redditPostId));
        Assert.True(await readRepository.ExistsForAnalysisRequestAsync(
            otherRequest.Id,
            redditPostId));
        Assert.False(await readRepository.ExistsForAnalysisRequestAsync(
            Guid.NewGuid(),
            redditPostId));
    }

    [Fact]
    public async Task AnalysisResultRepository_PersistsValidResult()
    {
        var request = IntegrationTestData.CreateRequest(AnalysisStatus.Processing);
        await SeedAsync(request);
        var result = IntegrationTestData.CreateResult(request.Id);

        await using (var writeContext = Fixture.CreateDbContext())
        {
            var repository = new AnalysisResultRepository(writeContext);
            await repository.AddAsync(result);
            await repository.SaveChangesAsync();
        }

        await using var readContext = Fixture.CreateDbContext();
        var persisted = await readContext.AnalysisResults
            .AsNoTracking()
            .SingleAsync(x => x.AnalysisRequestId == request.Id);
        Assert.Equal(result.Id, persisted.Id);
        Assert.Equal(result.Summary, persisted.Summary);
    }

    [Fact]
    public async Task AnalysisResultDatabaseConstraint_PreventsSecondResultForRequest()
    {
        var request = IntegrationTestData.CreateRequest(AnalysisStatus.Processing);
        var firstResult = IntegrationTestData.CreateResult(request.Id, summary: "First");
        await SeedAsync(request, firstResult);
        var secondResult = IntegrationTestData.CreateResult(request.Id, summary: "Second");

        await using var dbContext = Fixture.CreateDbContext();
        var repository = new AnalysisResultRepository(dbContext);
        await repository.AddAsync(secondResult);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => repository.SaveChangesAsync());

        await using var verifyContext = Fixture.CreateDbContext();
        Assert.Equal(
            1,
            await verifyContext.AnalysisResults.CountAsync(
                result => result.AnalysisRequestId == request.Id));
    }

    private async Task SeedAsync(params object[] entities)
    {
        await using var dbContext = Fixture.CreateDbContext();
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync();
    }
}
