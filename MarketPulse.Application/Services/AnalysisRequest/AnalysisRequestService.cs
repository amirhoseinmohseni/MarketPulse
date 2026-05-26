using MarketPulse.Application.Dtos;
using MarketPulse.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace MarketPulse.Application.Services.AnalysisRequest
{
    public class AnalysisRequestService : IAnalysisRequestService
    {
        private readonly IAnalysisRequestRepository _repository;
        private readonly IBackgroundTaskQueue _queue;
        private readonly ILogger<AnalysisRequestService> _logger;

        public AnalysisRequestService(
            IAnalysisRequestRepository repository,
            IBackgroundTaskQueue queue,
            ILogger<AnalysisRequestService> logger)
        {
            _repository = repository;
            _queue = queue;
            _logger = logger;
        }

        public async Task<Guid> CreateAnalysisRequest(CreateAnalysisRequestDto requestDto)
        {
            var requestId = Guid.NewGuid();

            _logger.LogInformation("Creating a new analysis request with ID: {RequestId}", requestId);

            var request = new Domain.Entities.AnalysisRequest
            {
                Id = requestId,
                Idea = requestDto.Idea,
                Status = Domain.Enums.AnalysisStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _repository.AddAsync(request);
                await _repository.SaveChangesAsync();

                _logger.LogDebug("Analysis request {RequestId} saved to database successfully.", requestId);

                await _queue.QueueBackgroundWorkItemAsync(request.Id);
                _logger.LogInformation("Analysis request {RequestId} queued for background processing.", requestId);

                return request.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create or queue analysis request for Idea: {IdeaPreview}",
                    requestDto.Idea?.Substring(0, Math.Min(requestDto.Idea.Length, 20)));
                throw;
            }
        }

        public async Task<AnalysisRequestDto?> GetAnalysisRequestByGuid(Guid requestId)
        {
            _logger.LogDebug("Fetching analysis request details for ID: {RequestId}", requestId);

            var request = await _repository.GetByIdWithResultAsync(requestId);

            if (request == null)
            {
                _logger.LogWarning("Analysis request with ID: {RequestId} not found in the repository.", requestId);
                return null;
            }

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
                _logger.LogInformation("Returning completed analysis result for ID: {RequestId}", requestId);

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
            else
            {
                _logger.LogInformation("Analysis request {RequestId} is still in status: {Status}", requestId, request.Status);
            }

            return requestDto;
        }
    }
}
