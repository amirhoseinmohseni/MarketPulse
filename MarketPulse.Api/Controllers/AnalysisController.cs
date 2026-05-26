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
        [ProducesResponseType(typeof(Guid), (int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<IActionResult> Create([FromBody] CreateAnalysisRequestDto requestDto)
        {
            try
            {
                // Validation - می توانید از FluentValidation هم استفاده کنید
                if (requestDto == null || string.IsNullOrWhiteSpace(requestDto.Idea))
                {
                    return BadRequest(new { Message = "Idea cannot be empty." });
                }

                var id = await _service.CreateAnalysisRequest(requestDto);

                // استفاده از CreatedAtAction به جای AcceptedAtAction برای رعایت استانداردهای REST
                // یا اگر فرآیند طولانی است، همان AcceptedAtAction مناسب است.
                return AcceptedAtAction(nameof(Get), new { id }, new { Id = id });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation failed for creating analysis request.");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating analysis request.");
                return StatusCode(500, new { Message = "An internal error occurred. Please try again later." });
            }
        }

        // GET: api/analysis/{id}
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AnalysisRequestDto), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<IActionResult> Get(Guid id)
        {
            try
            {
                var request = await _service.GetAnalysisRequestByGuid(id);

                if (request == null)
                {
                    _logger.LogWarning("Analysis request with ID {Id} not found.", id);
                    return NotFound(new { Message = $"Request with ID {id} was not found." });
                }

                return Ok(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving analysis request with ID {Id}.", id);
                return StatusCode(500, new { Message = "An error occurred while fetching the data." });
            }
        }
    }
}
