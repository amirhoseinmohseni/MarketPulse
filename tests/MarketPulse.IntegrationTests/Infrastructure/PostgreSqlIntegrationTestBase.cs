namespace MarketPulse.IntegrationTests.Infrastructure;

[Collection(PostgreSqlIntegrationCollection.Name)]
[Trait("Category", "Integration")]
public abstract class PostgreSqlIntegrationTestBase : IAsyncLifetime
{
    protected PostgreSqlIntegrationTestBase(PostgreSqlIntegrationFixture fixture)
    {
        Fixture = fixture;
    }

    protected PostgreSqlIntegrationFixture Fixture { get; }

    public Task InitializeAsync() => Fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
