using MarketPulse.Application;
using MarketPulse.Application.Dtos;
using MarketPulse.Application.Services.AnalysisRequest;
using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace MarketPulse.Api.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class AnalysisController : ControllerBase
    {
        private readonly IAnalysisRequestService _service;

        public AnalysisController(IAnalysisRequestService service)
        {
            _service = service;
        }

        // POST: api/analysis
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAnalysisRequestDto requestDto)
        {
            if (string.IsNullOrWhiteSpace(requestDto.Idea))
                return BadRequest("Idea cannot be empty.");

            var id = await _service.CreateAnalysisRequest(requestDto);

            return AcceptedAtAction(nameof(Get), new { id });
        }

        // GET: api/analysis/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var request = await _service.GetAnalysisRequestByGuid(id);

            if (request == null)
                return NotFound();

            return Ok(request);
        }
    }


}
