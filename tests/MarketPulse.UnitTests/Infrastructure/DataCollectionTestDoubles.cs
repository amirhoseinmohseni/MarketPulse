using System.Net;
using System.Text;
using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Repositories;

namespace MarketPulse.UnitTests.Infrastructure;

internal sealed class TestHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    : HttpMessageHandler
{
    private int _callCount;
    public int CallCount => _callCount;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        return handler(request, cancellationToken);
    }

    public static HttpResponseMessage JsonResponse(
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
}

internal sealed class RecordingCollectedMarketItemRepository
    : ICollectedMarketItemRepository
{
    public Func<Guid, string, string, bool> Exists { get; init; }
        = (_, _, _) => false;
    public List<(Guid RequestId, string Source, string ExternalId)> ExistsCalls { get; } = [];
    public List<CollectedMarketItem> Added { get; private set; } = [];
    public int AddCalls { get; private set; }
    public int SaveCalls { get; private set; }
    public CancellationToken AddToken { get; private set; }
    public CancellationToken SaveToken { get; private set; }

    public Task AddRangeAsync(
        IEnumerable<CollectedMarketItem> items,
        CancellationToken ct = default)
    {
        AddCalls++;
        AddToken = ct;
        Added = items.ToList();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CollectedMarketItem>> GetByAnalysisRequestIdAsync(
        Guid analysisRequestId,
        CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<bool> ExistsForAnalysisRequestAsync(
        Guid analysisRequestId,
        string source,
        string externalId,
        CancellationToken ct = default)
    {
        ExistsCalls.Add((analysisRequestId, source, externalId));
        return Task.FromResult(Exists(analysisRequestId, source, externalId));
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCalls++;
        SaveToken = ct;
        return Task.CompletedTask;
    }
}
