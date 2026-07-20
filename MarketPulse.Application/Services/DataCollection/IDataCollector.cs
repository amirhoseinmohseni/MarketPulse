namespace MarketPulse.Application.Services.DataCollection
{
    public interface IDataCollector
    {
        string SourceName { get; }

        Task<DataCollectionResult> CollectAsync(
            DataCollectionContext context,
            CancellationToken cancellationToken = default);
    }
}
