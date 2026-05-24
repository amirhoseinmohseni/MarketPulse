using MarketPulse.Application.Dtos;
using MarketPulse.Domain.Repositories;

namespace MarketPulse.Application.Services.AnalysisRequest
{
    public class AnalysisRequestService : IAnalysisRequestService
    {
        private readonly IAnalysisRequestRepository _repository;
        private readonly IBackgroundTaskQueue _queue;

        public AnalysisRequestService(IAnalysisRequestRepository repository, IBackgroundTaskQueue queue)
        {
            _repository = repository;
            _queue = queue;
        }

        public async Task<Guid> CreateAnalysisRequest(CreateAnalysisRequestDto requestDto)
        {
            var request = new Domain.Entities.AnalysisRequest
            {
                Id = Guid.NewGuid(),
                Idea = requestDto.Idea,
                Status = Domain.Enums.AnalysisStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(request);
            await _repository.SaveChangesAsync();

            await _queue.QueueBackgroundWorkItemAsync(request.Id);

            return request.Id;
        }

        public async Task<AnalysisRequestDto?> GetAnalysisRequestByGuid(Guid requestId)
        {
            var request = await _repository.GetByIdWithResultAsync(requestId);
            if (request == null) return null;

            var requestDto = new AnalysisRequestDto 
            {
                Id = requestId,
                CompletedAt = request.CompletedAt,
                CreatedAt = request.CreatedAt,
                Idea = request.Idea,
                Status = request.Status,
            };

            if (request.Result != null) 
            {
                requestDto.Result = new AnalysisResultDto
                {
                    AnalysisRequestId = requestId,
                    Id = request.Result.Id,
                    MarketScore = request.Result.MarketScore,
                    Opportunities = request.Result.Opportunities,
                    Risks = request.Result.Risks,
                    Strengths = request.Result.Strengths,
                    Summary = request.Result.Summary,
                    Weaknesses = request.Result.Weaknesses
                };
            }

            return requestDto;
        }
    }
}
