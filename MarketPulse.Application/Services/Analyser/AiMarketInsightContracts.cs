using System.Text.Json.Serialization;

namespace MarketPulse.Application.Services.Analyser;

public sealed record AiMarketInsightRequest(
    string SystemPrompt,
    string UserPrompt,
    string ResponseSchemaName,
    string ResponseJsonSchema);

public sealed record AiMarketInsightInput(
    string Idea,
    IReadOnlyList<AiMarketInsightInputItem> Items);

public sealed record AiMarketInsightInputItem(
    string EvidenceId,
    string Source,
    string Title,
    string Content,
    int? Score,
    int? CommentCount,
    DateTime? CreatedUtc);

public sealed class AiMarketInsightResponse
{
    [JsonPropertyName("marketScore")]
    public int? MarketScore { get; init; }

    [JsonPropertyName("signalStrength")]
    public required string SignalStrength { get; init; }

    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    [JsonPropertyName("strengths")]
    public required List<AiMarketInsightItem> Strengths { get; init; }

    [JsonPropertyName("weaknesses")]
    public required List<AiMarketInsightItem> Weaknesses { get; init; }

    [JsonPropertyName("opportunities")]
    public required List<AiMarketInsightItem> Opportunities { get; init; }

    [JsonPropertyName("risks")]
    public required List<AiMarketInsightItem> Risks { get; init; }
}

public sealed class AiMarketInsightItem
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("evidenceIds")]
    public required List<string> EvidenceIds { get; init; }
}
