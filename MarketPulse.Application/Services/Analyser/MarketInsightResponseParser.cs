using System.Text.Json;
using System.Text.Json.Serialization;
using MarketPulse.Domain.Enums;

namespace MarketPulse.Application.Services.Analyser;

public interface IMarketInsightResponseParser
{
    ValidatedMarketInsightResponse ParseAndValidate(
        string json,
        IReadOnlyDictionary<string, Guid> evidenceMap,
        SignalStrength maximumSignalStrength);
}

public sealed record ValidatedMarketInsightResponse(
    int? MarketScore,
    SignalStrength SignalStrength,
    string Summary,
    IReadOnlyList<ValidatedMarketInsight> Strengths,
    IReadOnlyList<ValidatedMarketInsight> Weaknesses,
    IReadOnlyList<ValidatedMarketInsight> Opportunities,
    IReadOnlyList<ValidatedMarketInsight> Risks);

public sealed record ValidatedMarketInsight(
    string Text,
    IReadOnlyList<Guid> CollectedMarketItemIds);

public sealed class MarketInsightResponseParser : IMarketInsightResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly MarketInsightAnalysisOptions _options;

    public MarketInsightResponseParser(MarketInsightAnalysisOptions options)
    {
        _options = options;
    }

    public ValidatedMarketInsightResponse ParseAndValidate(
        string json,
        IReadOnlyDictionary<string, Guid> evidenceMap,
        SignalStrength maximumSignalStrength)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidAiMarketInsightResponseException("The AI response was empty.");
        }

        AiMarketInsightResponse response;
        try
        {
            response = JsonSerializer.Deserialize<AiMarketInsightResponse>(json, JsonOptions)
                ?? throw new InvalidAiMarketInsightResponseException("The AI response was null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidAiMarketInsightResponseException(
                "The AI response was not valid market-insight JSON.",
                exception);
        }

        if (response.MarketScore is < 0 or > 100)
        {
            throw new InvalidAiMarketInsightResponseException(
                "MarketScore must be null or between 0 and 100.");
        }

        if (!Enum.TryParse<SignalStrength>(response.SignalStrength, ignoreCase: false, out var modelSignal)
            || !Enum.IsDefined(modelSignal))
        {
            throw new InvalidAiMarketInsightResponseException(
                "SignalStrength must be Weak, Moderate, or Strong.");
        }

        var summary = response.Summary?.Trim();
        if (string.IsNullOrWhiteSpace(summary) || summary.Length > _options.MaxSummaryLength)
        {
            throw new InvalidAiMarketInsightResponseException(
                $"Summary must contain between 1 and {_options.MaxSummaryLength} characters.");
        }

        var strengths = ValidateInsights(response.Strengths, evidenceMap, "strengths");
        var weaknesses = ValidateInsights(response.Weaknesses, evidenceMap, "weaknesses");
        var opportunities = ValidateInsights(response.Opportunities, evidenceMap, "opportunities");
        var risks = ValidateInsights(response.Risks, evidenceMap, "risks");

        var insightCount = strengths.Count + weaknesses.Count + opportunities.Count + risks.Count;
        if (insightCount < _options.MinInsightCount)
        {
            throw new InvalidAiMarketInsightResponseException(
                $"The AI response must contain at least {_options.MinInsightCount} insight.");
        }

        var enforcedSignal = modelSignal > maximumSignalStrength
            ? maximumSignalStrength
            : modelSignal;
        var enforcedScore = enforcedSignal == SignalStrength.Weak
            ? null
            : response.MarketScore;

        return new ValidatedMarketInsightResponse(
            enforcedScore,
            enforcedSignal,
            summary,
            strengths,
            weaknesses,
            opportunities,
            risks);
    }

    private IReadOnlyList<ValidatedMarketInsight> ValidateInsights(
        IReadOnlyList<AiMarketInsightItem>? insights,
        IReadOnlyDictionary<string, Guid> evidenceMap,
        string category)
    {
        if (insights is null)
        {
            throw new InvalidAiMarketInsightResponseException(
                $"The {category} collection is required.");
        }

        if (insights.Count > _options.MaxInsightsPerCategory)
        {
            throw new InvalidAiMarketInsightResponseException(
                $"The {category} collection exceeds the configured limit.");
        }

        var validated = new List<ValidatedMarketInsight>(insights.Count);
        foreach (var insight in insights)
        {
            var text = insight.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Length > _options.MaxInsightTextLength)
            {
                throw new InvalidAiMarketInsightResponseException(
                    $"Every {category} insight must have valid non-empty text.");
            }

            if (insight.EvidenceIds is null
                || insight.EvidenceIds.Count == 0
                || insight.EvidenceIds.Count > _options.MaxEvidencePerInsight)
            {
                throw new InvalidAiMarketInsightResponseException(
                    $"Every {category} insight must reference a valid number of evidence IDs.");
            }

            if (insight.EvidenceIds.Distinct(StringComparer.Ordinal).Count() != insight.EvidenceIds.Count)
            {
                throw new InvalidAiMarketInsightResponseException(
                    $"An insight in {category} contains duplicate evidence IDs.");
            }

            var collectedMarketItemIds = new List<Guid>(insight.EvidenceIds.Count);
            foreach (var evidenceId in insight.EvidenceIds)
            {
                if (string.IsNullOrWhiteSpace(evidenceId)
                    || !evidenceMap.TryGetValue(evidenceId, out var collectedMarketItemId))
                {
                    throw new InvalidAiMarketInsightResponseException(
                        $"The evidence ID '{evidenceId}' is not part of this analysis input.");
                }

                collectedMarketItemIds.Add(collectedMarketItemId);
            }

            validated.Add(new ValidatedMarketInsight(text, collectedMarketItemIds));
        }

        return validated;
    }
}
