using MarketPulse.Application;
using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace MarketPulse.Api.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class AnalysisController : ControllerBase
    {
        private readonly IAnalysisRequestRepository _repository;
        private readonly IBackgroundTaskQueue _queue;

        public AnalysisController(IAnalysisRequestRepository repository, IBackgroundTaskQueue queue)
        {
            _repository = repository;
            _queue = queue;
        }

        // POST: api/analysis
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAnalysisRequestDto requestDto)
        {
            if (string.IsNullOrWhiteSpace(requestDto.Idea))
                return BadRequest("Idea cannot be empty.");

            var request = new AnalysisRequest
            {
                Id = Guid.NewGuid(),
                Idea = requestDto.Idea,
                Status = Domain.Enums.AnalysisStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(request);
            await _repository.SaveChangesAsync();

            await _queue.QueueBackgroundWorkItemAsync(request.Id);

            return AcceptedAtAction(nameof(Get), new { id = request.Id }, new { request.Id, request.Status });
        }

        // GET: api/analysis/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var request = await _repository.GetByIdWithResultAsync(id);

            if (request == null)
                return NotFound();

            return Ok(request);
        }
    }

    public record CreateAnalysisRequestDto(string Idea);

}
