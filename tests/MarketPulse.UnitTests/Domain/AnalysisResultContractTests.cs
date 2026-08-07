using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Enums;

namespace MarketPulse.UnitTests.Domain;

public class AnalysisResultContractTests
{
    [Fact]
    public void NewResult_UsesHonestWeakSignalDefaults()
    {
        var result = new AnalysisResult();

        Assert.Null(result.MarketScore);
        Assert.Equal(SignalStrength.Weak, result.SignalStrength);
        Assert.Empty(result.Insights);
    }

    [Fact]
    public void Insight_CanReferenceACollectedMarketItemThroughEvidence()
    {
        var item = new CollectedMarketItem
        {
            Id = Guid.NewGuid(),
            Source = "HackerNews",
            ExternalId = "item-1",
            Title = "Market signal"
        };
        var insight = new AnalysisInsight
        {
            Id = Guid.NewGuid(),
            Type = InsightType.Opportunity,
            Position = 0,
            Text = "Users are looking for a simpler workflow."
        };
        var evidence = new AnalysisEvidence
        {
            Id = Guid.NewGuid(),
            AnalysisInsightId = insight.Id,
            AnalysisInsight = insight,
            CollectedMarketItemId = item.Id,
            CollectedMarketItem = item
        };

        insight.Evidence.Add(evidence);
        item.AnalysisEvidence.Add(evidence);

        Assert.Same(item, insight.Evidence.Single().CollectedMarketItem);
        Assert.Equal(item.Id, insight.Evidence.Single().CollectedMarketItemId);
    }
}
