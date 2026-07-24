using MarketPulse.Application;
using MarketPulse.Application.Dtos;
using MarketPulse.Application.Services.AnalysisRequest;
using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Enums;
using MarketPulse.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketPulse.UnitTests.Application;

public class AnalysisRequestServiceTests
{
    [Fact]
    public async Task GetAnalysisRequest_MapsStructuredInsightsAndDatabaseEvidence()
    {
        var requestId = Guid.NewGuid();
        var resultId = Guid.NewGuid();
        var item = new CollectedMarketItem
        {
            Id = Guid.NewGuid(),
            AnalysisRequestId = requestId,
            Source = "HackerNews",
            ExternalId = "hn-1",
            Title = "Users discuss this problem",
            Url = "https://example.test/item",
            Permalink = "https://example.test/item/permalink"
        };
        var strength = new AnalysisInsight
        {
            Id = Guid.NewGuid(),
            AnalysisResultId = resultId,
            Type = InsightType.Strength,
            Position = 0,
            Text = "The problem receives repeated engagement."
        };
        strength.Evidence.Add(new AnalysisEvidence
        {
            Id = Guid.NewGuid(),
            AnalysisInsightId = strength.Id,
            CollectedMarketItemId = item.Id,
            CollectedMarketItem = item
        });

        var request = new AnalysisRequest
        {
            Id = requestId,
            Idea = "A test idea",
            Status = AnalysisStatus.Completed,
            Result = new AnalysisResult
            {
                Id = resultId,
                AnalysisRequestId = requestId,
                MarketScore = 64,
                SignalStrength = SignalStrength.Moderate,
                Summary = "A grounded summary.",
                Insights = [strength]
            }
        };
        var repository = new StubAnalysisRequestRepository(request);
        var service = new AnalysisRequestService(
            repository,
            new StubBackgroundTaskQueue(),
            NullLogger<AnalysisRequestService>.Instance);

        var dto = await service.GetAnalysisRequestByGuid(requestId);

        Assert.NotNull(dto?.Result);
        Assert.Equal(64, dto.Result.MarketScore);
        Assert.Equal(SignalStrength.Moderate, dto.Result.SignalStrength);
        var mappedStrength = Assert.Single(dto.Result.Strengths);
        Assert.Equal(strength.Text, mappedStrength.Text);
        Assert.Equal(item.Id, Assert.Single(mappedStrength.EvidenceIds));
        var mappedEvidence = Assert.Single(dto.Result.Evidence);
        Assert.Equal(item.Id, mappedEvidence.CollectedMarketItemId);
        Assert.Equal(item.Source, mappedEvidence.Source);
        Assert.Equal(item.Title, mappedEvidence.Title);
    }

    private sealed class StubAnalysisRequestRepository(AnalysisRequest request)
        : IAnalysisRequestRepository
    {
        public Task<AnalysisRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<AnalysisRequest?>(request.Id == id ? request : null);

        public Task<AnalysisRequest?> GetByIdWithResultAsync(Guid id, CancellationToken ct = default)
            => GetByIdAsync(id, ct);

        public Task AddAsync(AnalysisRequest analysisRequest, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task UpdateAsync(AnalysisRequest analysisRequest, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubBackgroundTaskQueue : IBackgroundTaskQueue
    {
        public ValueTask QueueBackgroundWorkItemAsync(Guid requestId)
            => ValueTask.CompletedTask;

        public ValueTask<Guid> DequeueAsync(CancellationToken ct)
            => ValueTask.FromResult(Guid.Empty);
    }
}
