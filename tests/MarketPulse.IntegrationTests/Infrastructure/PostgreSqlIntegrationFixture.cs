using System.Security.Cryptography;
using MarketPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace MarketPulse.IntegrationTests.Infrastructure;

public sealed class PostgreSqlIntegrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("marketpulse_integration")
        .WithUsername("marketpulse_integration")
        .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(24)))
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .EnableDetailedErrors()
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task ResetDatabaseAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                "AnalysisEvidences",
                "AnalysisInsights",
                "AnalysisResults",
                "CollectedMarketItems",
                "RedditPosts",
                "SearchQueries",
                "AnalysisRequests"
            RESTART IDENTITY CASCADE;
            """);
    }
}
