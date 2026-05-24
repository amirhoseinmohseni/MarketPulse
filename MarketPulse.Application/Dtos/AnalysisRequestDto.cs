using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Enums;

namespace MarketPulse.Application.Dtos
{
    public class AnalysisRequestDto
    {
        public Guid Id { get; set; }
        public string Idea { get; set; } = string.Empty;
        public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public AnalysisResultDto? Result { get; set; }
    }
}
