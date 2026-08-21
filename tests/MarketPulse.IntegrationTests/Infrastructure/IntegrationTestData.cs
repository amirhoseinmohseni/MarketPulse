using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Enums;

namespace MarketPulse.IntegrationTests.Infrastructure;

internal static class IntegrationTestData
{
    public static AnalysisRequest CreateRequest(
        AnalysisStatus status = AnalysisStatus.Pending,
        DateTime? completedAt = null,
        string idea = "Integration test idea")
        => new()
        {
            Id = Guid.NewGuid(),
            Idea = idea,
            Status = status,
            CreatedAt = new DateTime(2026, 8, 21, 8, 0, 0, DateTimeKind.Utc),
            CompletedAt = completedAt
        };

    public static SearchQuery CreateSearchQuery(
        Guid analysisRequestId,
        int priority = 1,
        string? query = null,
        QueryCategory category = QueryCategory.Problem)
        => new()
        {
            Id = Guid.NewGuid(),
            AnalysisRequestId = analysisRequestId,
            Query = query ?? $"query-{Guid.NewGuid():N}",
            Category = category,
            Priority = priority
        };

    public static CollectedMarketItem CreateCollectedItem(
        Guid analysisRequestId,
        Guid? searchQueryId = null,
        string source = "HackerNews",
        string? externalId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            AnalysisRequestId = analysisRequestId,
            SearchQueryId = searchQueryId,
            Source = source,
            ExternalId = externalId ?? $"external-{Guid.NewGuid():N}",
            Title = "Collected integration item",
            Content = "Collected content",
            Url = "https://example.test/item",
            Permalink = "https://example.test/item/permalink",
            Score = 12,
            CommentCount = 3,
            CreatedUtc = new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc),
            CollectedAt = new DateTime(2026, 8, 21, 8, 30, 0, DateTimeKind.Utc)
        };

    public static RedditPost CreateRedditPost(
        Guid analysisRequestId,
        Guid? searchQueryId = null,
        string? redditPostId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            AnalysisRequestId = analysisRequestId,
            SearchQueryId = searchQueryId,
            RedditPostId = redditPostId ?? $"reddit-{Guid.NewGuid():N}",
            Subreddit = "startups",
            Title = "Integration Reddit post",
            SelfText = "Reddit content",
            Url = "https://example.test/reddit",
            Permalink = "/r/startups/comments/integration",
            Score = 8,
            CommentCount = 2,
            CreatedUtc = new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
            CollectedAt = new DateTime(2026, 8, 21, 9, 0, 0, DateTimeKind.Utc)
        };

    public static AnalysisResult CreateResult(
        Guid analysisRequestId,
        Guid? collectedMarketItemId = null,
        string summary = "Integration result")
    {
        var resultId = Guid.NewGuid();
        var result = new AnalysisResult
        {
            Id = resultId,
            AnalysisRequestId = analysisRequestId,
            MarketScore = null,
            SignalStrength = SignalStrength.Weak,
            Summary = summary
        };

        if (collectedMarketItemId is null)
        {
            return result;
        }

        var insightId = Guid.NewGuid();
        result.Insights.Add(new AnalysisInsight
        {
            Id = insightId,
            AnalysisResultId = resultId,
            Type = InsightType.Opportunity,
            Position = 0,
            Text = "Grounded opportunity",
            Evidence =
            [
                new AnalysisEvidence
                {
                    Id = Guid.NewGuid(),
                    AnalysisInsightId = insightId,
                    CollectedMarketItemId = collectedMarketItemId.Value
                }
            ]
        });

        return result;
    }
}
