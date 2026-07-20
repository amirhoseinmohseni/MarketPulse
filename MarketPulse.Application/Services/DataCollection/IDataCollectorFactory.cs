namespace MarketPulse.Application.Services.DataCollection
{
    public interface IDataCollectorFactory
    {
        IReadOnlyList<IDataCollector> GetEnabledCollectors();
    }
}
