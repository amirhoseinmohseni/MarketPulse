using MarketPulse.Api.Controllers;
using MarketPulse.Application.Dtos;
using MarketPulse.Application.Services.AnalysisRequest;
using MarketPulse.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketPulse.UnitTests.Api;

public class AnalysisControllerTests
{
    [Fact]
    public async Task Get_ReturnsCompletedMarketInsightContractIncludingEvidenceReason()
    {
        var requestId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var dto = new AnalysisRequestDto
        {
            Id = requestId,
            Idea = "A test idea",
            Status = AnalysisStatus.Completed,
            Result = new AnalysisResultDto
            {
                Id = Guid.NewGuid(),
                AnalysisRequestId = requestId,
                SignalStrength = SignalStrength.Weak,
                MarketScore = null,
                Summary = "A grounded weak result.",
                Evidence =
                [
                    new AnalysisEvidenceDto
                    {
                        CollectedMarketItemId = evidenceId,
                        Source = "HackerNews",
                        Title = "Database title",
                        Url = "https://example.test/item",
                        Permalink = "https://example.test/permalink",
                        Reason = "This item supports the reported insight."
                    }
                ]
            }
        };
        var controller = new AnalysisController(
            new StubAnalysisRequestService(dto),
            NullLogger<AnalysisController>.Instance);

        var actionResult = await controller.Get(requestId);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<AnalysisRequestDto>(ok.Value);
        var evidence = Assert.Single(response.Result!.Evidence);
        Assert.Equal(evidenceId, evidence.CollectedMarketItemId);
        Assert.Equal("HackerNews", evidence.Source);
        Assert.Equal("Database title", evidence.Title);
        Assert.Equal("https://example.test/item", evidence.Url);
        Assert.Equal("https://example.test/permalink", evidence.Permalink);
        Assert.Equal("This item supports the reported insight.", evidence.Reason);
    }

    private sealed class StubAnalysisRequestService(AnalysisRequestDto dto)
        : IAnalysisRequestService
    {
        public Task<Guid> CreateAnalysisRequest(CreateAnalysisRequestDto requestDto)
            => Task.FromResult(dto.Id);

        public Task<AnalysisRequestDto?> GetAnalysisRequestByGuid(Guid requestId)
            => Task.FromResult<AnalysisRequestDto?>(
                requestId == dto.Id ? dto : null);
    }
}
