namespace MarketPulse.Application.Services.Analyser;

public sealed class MarketInsightAnalysisOptions
{
    public const string SectionName = "MarketInsightAnalysis";

    public int MaxItems { get; init; } = 20;
    public int MaxIdeaLength { get; init; } = 500;
    public int MaxSourceLength { get; init; } = 100;
    public int MaxTitleLength { get; init; } = 240;
    public int MaxContentLengthPerItem { get; init; } = 2_000;
    public int MaxTotalItemCharacters { get; init; } = 16_000;
    public int MinUsableTextLength { get; init; } = 20;
    public int ModerateMinItems { get; init; } = 4;
    public int ModerateMinSources { get; init; } = 2;
    public int StrongMinItems { get; init; } = 10;
    public int StrongMinSources { get; init; } = 3;
    public int MinInsightCount { get; init; } = 1;
    public int MaxInsightsPerCategory { get; init; } = 5;
    public int MaxEvidencePerInsight { get; init; } = 5;
    public int MaxSummaryLength { get; init; } = 2_000;
    public int MaxInsightTextLength { get; init; } = 1_000;
}
