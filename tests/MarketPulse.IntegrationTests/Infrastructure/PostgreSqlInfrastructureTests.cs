using MarketPulse.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarketPulse.IntegrationTests.Infrastructure;

public sealed class PostgreSqlInfrastructureTests(PostgreSqlIntegrationFixture fixture)
    : PostgreSqlIntegrationTestBase(fixture)
{
    [Fact]
    public async Task Container_IsReachableAndAllMigrationsAreApplied()
    {
        await using var dbContext = Fixture.CreateDbContext();

        var knownMigrations = dbContext.Database.GetMigrations().ToArray();
        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();

        Assert.True(await dbContext.Database.CanConnectAsync());
        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(knownMigrations);
        Assert.Equal(knownMigrations, appliedMigrations);
    }

    [Fact]
    public async Task MigratedSchema_CanPersistAndReadAnAnalysisRequest()
    {
        var request = IntegrationTestData.CreateRequest();

        await using (var writeContext = Fixture.CreateDbContext())
        {
            writeContext.AnalysisRequests.Add(request);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = Fixture.CreateDbContext();
        var persisted = await readContext.AnalysisRequests
            .AsNoTracking()
            .SingleAsync(x => x.Id == request.Id);

        Assert.Equal(AnalysisStatus.Pending, persisted.Status);
        Assert.Equal(request.Idea, persisted.Idea);
    }
}
