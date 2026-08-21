using MarketPulse.Application.Services.SearchQueryGenerator;
using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Repositories;

namespace MarketPulse.UnitTests.Application;

public class SearchQueryGenerationServiceTests
{
    [Fact]
    public async Task Generate_AssignsRequestIdAndPersistsInOrder()
    {
        var requestId = Guid.NewGuid();
        var queries = new List<SearchQuery>
        {
            Query("first"),
            Query("second")
        };
        var generator = new StubGenerator(queries);
        var repository = new RecordingRepository();
        var service = new SearchQueryGenerationService(generator, repository);

        await service.GenerateForAnalysisRequestAsync(requestId, "idea");

        Assert.Equal(new[] { "AddRange", "Save" }, repository.Calls);
        Assert.Equal(queries, repository.Added);
        Assert.All(repository.Added, query => Assert.Equal(requestId, query.AnalysisRequestId));
    }

    [Fact]
    public async Task Generate_WhenGeneratorReturnsEmpty_PersistsEmptySetWithoutCreatingEntities()
    {
        var repository = new RecordingRepository();
        var service = new SearchQueryGenerationService(
            new StubGenerator([]),
            repository);

        await service.GenerateForAnalysisRequestAsync(Guid.NewGuid(), "idea");

        Assert.Empty(repository.Added);
        Assert.Equal(new[] { "AddRange", "Save" }, repository.Calls);
    }

    [Fact]
    public async Task Generate_PropagatesCancellationTokenToAllDependencies()
    {
        using var source = new CancellationTokenSource();
        var repository = new RecordingRepository();
        var generator = new StubGenerator([Query("one")]);
        var service = new SearchQueryGenerationService(generator, repository);

        await service.GenerateForAnalysisRequestAsync(Guid.NewGuid(), "idea", source.Token);

        Assert.Equal(source.Token, generator.Token);
        Assert.Equal(source.Token, repository.AddToken);
        Assert.Equal(source.Token, repository.SaveToken);
    }

    [Fact]
    public async Task Generate_WhenGeneratorFails_DoesNotPersist()
    {
        var repository = new RecordingRepository();
        var service = new SearchQueryGenerationService(
            new StubGenerator(new InvalidOperationException("generation failed")),
            repository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GenerateForAnalysisRequestAsync(Guid.NewGuid(), "idea"));

        Assert.Empty(repository.Calls);
    }

    [Fact]
    public async Task Generate_WhenAddRangeFails_DoesNotSave()
    {
        var repository = new RecordingRepository { ThrowOnAdd = true };
        var service = new SearchQueryGenerationService(
            new StubGenerator([Query("one")]),
            repository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GenerateForAnalysisRequestAsync(Guid.NewGuid(), "idea"));

        Assert.Equal(new[] { "AddRange" }, repository.Calls);
    }

    private static SearchQuery Query(string text)
        => new() { Id = Guid.NewGuid(), Query = text, Priority = 1 };

    private sealed class StubGenerator : ISearchQueryGenerator
    {
        private readonly List<SearchQuery>? _queries;
        private readonly Exception? _exception;

        public StubGenerator(List<SearchQuery> queries) => _queries = queries;
        public StubGenerator(Exception exception) => _exception = exception;
        public CancellationToken Token { get; private set; }

        public Task<List<SearchQuery>> GenerateAsync(
            string idea,
            CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            return _exception is null
                ? Task.FromResult(_queries!)
                : Task.FromException<List<SearchQuery>>(_exception);
        }
    }

    private sealed class RecordingRepository : ISearchQueryRepository
    {
        public List<string> Calls { get; } = [];
        public List<SearchQuery> Added { get; private set; } = [];
        public bool ThrowOnAdd { get; init; }
        public CancellationToken AddToken { get; private set; }
        public CancellationToken SaveToken { get; private set; }

        public Task AddRangeAsync(
            IEnumerable<SearchQuery> searchQueries,
            CancellationToken ct = default)
        {
            Calls.Add("AddRange");
            AddToken = ct;
            if (ThrowOnAdd)
            {
                throw new InvalidOperationException("add failed");
            }

            Added = searchQueries.ToList();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SearchQuery>> GetByAnalysisRequestIdAsync(
            Guid analysisRequestId,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            Calls.Add("Save");
            SaveToken = ct;
            return Task.CompletedTask;
        }
    }
}
