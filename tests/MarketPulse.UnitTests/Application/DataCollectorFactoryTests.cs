using MarketPulse.Application.Services.DataCollection;

namespace MarketPulse.UnitTests.Application;

public class DataCollectorFactoryTests
{
    [Fact]
    public void GetEnabledCollectors_FiltersByConfigurationCaseInsensitivelyAndPreservesOrder()
    {
        var first = new StubCollector("HackerNews");
        var disabled = new StubCollector("Disabled");
        var unconfigured = new StubCollector("Unconfigured");
        var last = new StubCollector("Reddit");
        var options = new DataCollectorOptions();
        options.Sources["hackernews"] = new DataCollectorSourceOptions { Enabled = true };
        options.Sources["Disabled"] = new DataCollectorSourceOptions { Enabled = false };
        options.Sources["REDDIT"] = new DataCollectorSourceOptions { Enabled = true };
        var factory = new DataCollectorFactory(
            [first, disabled, unconfigured, last],
            options);

        var result = factory.GetEnabledCollectors();

        Assert.Equal(new IDataCollector[] { first, last }, result);
    }

    [Fact]
    public void GetEnabledCollectors_WhenNoneAreEnabled_ReturnsEmptyCollection()
    {
        var options = new DataCollectorOptions();
        options.Sources["HackerNews"] = new DataCollectorSourceOptions { Enabled = false };
        var factory = new DataCollectorFactory(
            [new StubCollector("HackerNews"), new StubCollector("Missing")],
            options);

        Assert.Empty(factory.GetEnabledCollectors());
    }

    private sealed class StubCollector(string sourceName) : IDataCollector
    {
        public string SourceName => sourceName;

        public Task<DataCollectionResult> CollectAsync(
            DataCollectionContext context,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
