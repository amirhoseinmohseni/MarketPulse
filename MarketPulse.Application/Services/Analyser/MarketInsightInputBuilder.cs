using MarketPulse.Domain.Entities;

namespace MarketPulse.Application.Services.Analyser;

public interface IMarketInsightInputBuilder
{
    MarketInsightInputBuildResult Build(
        Guid analysisRequestId,
        string idea,
        IReadOnlyList<CollectedMarketItem> collectedItems);
}

public sealed record MarketInsightInputBuildResult(
    AiMarketInsightInput Input,
    IReadOnlyDictionary<string, Guid> EvidenceMap);

public sealed class MarketInsightInputBuilder : IMarketInsightInputBuilder
{
    private readonly MarketInsightAnalysisOptions _options;

    public MarketInsightInputBuilder(MarketInsightAnalysisOptions options)
    {
        _options = options;
    }

    public MarketInsightInputBuildResult Build(
        Guid analysisRequestId,
        string idea,
        IReadOnlyList<CollectedMarketItem> collectedItems)
    {
        ArgumentNullException.ThrowIfNull(collectedItems);

        var candidates = collectedItems
            .Where(item => item.AnalysisRequestId == analysisRequestId)
            .Where(IsUsable)
            .OrderByDescending(item => item.Score ?? int.MinValue)
            .ThenByDescending(item => item.CommentCount ?? int.MinValue)
            .ThenByDescending(item => item.CreatedUtc ?? DateTime.MinValue)
            .ThenBy(item => item.Source, StringComparer.Ordinal)
            .ThenBy(item => item.ExternalId, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .ToList();

        var inputItems = new List<AiMarketInsightInputItem>();
        var evidenceMap = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var remainingCharacters = _options.MaxTotalItemCharacters;

        foreach (var item in candidates)
        {
            if (inputItems.Count >= _options.MaxItems || remainingCharacters <= 0)
            {
                break;
            }

            var source = Truncate(
                Normalize(item.Source),
                Math.Min(_options.MaxSourceLength, remainingCharacters));
            var titleLimit = Math.Min(
                _options.MaxTitleLength,
                remainingCharacters - source.Length);
            var title = titleLimit > 0
                ? Truncate(Normalize(item.Title), titleLimit)
                : string.Empty;
            var contentLimit = Math.Min(
                _options.MaxContentLengthPerItem,
                remainingCharacters - source.Length - title.Length);
            var content = contentLimit > 0
                ? Truncate(Normalize(item.Content), contentLimit)
                : string.Empty;

            if ((title.Length + content.Length) < _options.MinUsableTextLength)
            {
                continue;
            }

            var evidenceId = $"C{inputItems.Count + 1:000}";
            inputItems.Add(new AiMarketInsightInputItem(
                evidenceId,
                source,
                title,
                content,
                item.Score,
                item.CommentCount,
                item.CreatedUtc));
            evidenceMap.Add(evidenceId, item.Id);
            remainingCharacters -= source.Length + title.Length + content.Length;
        }

        var input = new AiMarketInsightInput(
            Truncate(Normalize(idea), _options.MaxIdeaLength),
            inputItems);

        return new MarketInsightInputBuildResult(input, evidenceMap);
    }

    private bool IsUsable(CollectedMarketItem item)
    {
        if (item.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(item.Source))
        {
            return false;
        }

        var textLength = Normalize(item.Title).Length + Normalize(item.Content).Length;
        return textLength >= _options.MinUsableTextLength;
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
