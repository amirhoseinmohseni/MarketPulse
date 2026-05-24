using MarketPulse.Application.Dtos;
using MarketPulse.Domain.Repositories;

namespace MarketPulse.Application.Services.AnalysisRequest
{
    public interface IAnalysisRequestService
    {
        Task<Guid> CreateAnalysisRequest(CreateAnalysisRequestDto requestDto);
        Task<AnalysisRequestDto?> GetAnalysisRequestByGuid(Guid requestId);
    }
}
