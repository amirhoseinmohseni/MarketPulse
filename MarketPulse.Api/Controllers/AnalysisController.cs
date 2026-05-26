using MarketPulse.Application.Dtos;
using MarketPulse.Application.Services.AnalysisRequest;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace MarketPulse.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalysisController : ControllerBase
    {
        private readonly IAnalysisRequestService _service;
        private readonly ILogger<AnalysisController> _logger;

        public AnalysisController(IAnalysisRequestService service, ILogger<AnalysisController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // POST: api/analysis
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAnalysisRequestDto requestDto)
        {
            _logger.LogInformation("Attempting to create a new analysis request. Idea: {IdeaPreview}...",
                requestDto.Idea?.Substring(0, Math.Min(requestDto.Idea.Length, 20)));

            try
            {
                if (string.IsNullOrWhiteSpace(requestDto.Idea))
                {
                    _logger.LogWarning("Validation failed: Idea field is empty.");
                    return BadRequest(new { Message = "Idea cannot be empty." });
                }

                var id = await _service.CreateAnalysisRequest(requestDto);

                _logger.LogInformation("Successfully created analysis request with ID: {Id}", id);

                return AcceptedAtAction(nameof(Get), new { id }, new { Id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating analysis request.");

                return StatusCode((int)HttpStatusCode.InternalServerError,
                    new { Message = "An internal error occurred while processing your request." });
            }
        }

        // GET: api/analysis/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            _logger.LogInformation("Fetching analysis request for ID: {Id}", id);

            try
            {
                var request = await _service.GetAnalysisRequestByGuid(id);

                if (request == null)
                {
                    _logger.LogWarning("Analysis request with ID: {Id} was not found.", id);
                    return NotFound(new { Message = $"No analysis found for ID: {id}" });
                }

                _logger.LogInformation("Successfully retrieved analysis request for ID: {Id}", id);
                return Ok(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving analysis request with ID: {Id}", id);

                return StatusCode((int)HttpStatusCode.InternalServerError,
                    new { Message = "An error occurred while fetching the data." });
            }
        }
    }
}
