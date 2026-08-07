using MarketPulse.Domain.Enums;

namespace MarketPulse.Application.Services.Analyser;

public interface IMarketInsightSignalEvaluator
{
    MarketInsightSignalAssessment Evaluate(IReadOnlyList<AiMarketInsightInputItem> items);
}

public sealed record MarketInsightSignalAssessment(
    SignalStrength MaximumSignalStrength,
    string? WeakSignalSummary);

public sealed class MarketInsightSignalEvaluator : IMarketInsightSignalEvaluator
{
    private readonly MarketInsightAnalysisOptions _options;

    public MarketInsightSignalEvaluator(MarketInsightAnalysisOptions options)
    {
        _options = options;
    }

    public MarketInsightSignalAssessment Evaluate(IReadOnlyList<AiMarketInsightInputItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var sourceCount = items
            .Select(item => item.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (items.Count >= _options.StrongMinItems
            && sourceCount >= _options.StrongMinSources)
        {
            return new MarketInsightSignalAssessment(SignalStrength.Strong, null);
        }

        if (items.Count >= _options.ModerateMinItems
            && sourceCount >= _options.ModerateMinSources)
        {
            return new MarketInsightSignalAssessment(SignalStrength.Moderate, null);
        }

        var reason = sourceCount <= 1
            ? "The available evidence is limited in volume or source diversity, so the market signal is weak."
            : "The available evidence is limited in volume, so the market signal is weak.";

        return new MarketInsightSignalAssessment(SignalStrength.Weak, reason);
    }
}
