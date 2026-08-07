using MarketPulse.Domain.Entities;
using MarketPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MarketPulse.UnitTests.Persistence;

public class ApplicationDbContextModelTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql()
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public void AnalysisResultMapping_UsesNullableScore()
    {
        using var dbContext = CreateDbContext();
        var entity = dbContext.Model.FindEntityType(typeof(AnalysisResult));

        Assert.NotNull(entity);
        Assert.True(entity.FindProperty(nameof(AnalysisResult.MarketScore))!.IsNullable);
    }

    [Fact]
    public void AnalysisResultMapping_EnforcesOneResultPerRequest()
    {
        using var dbContext = CreateDbContext();
        var entity = dbContext.Model.FindEntityType(typeof(AnalysisResult));

        Assert.NotNull(entity);
        var requestForeignKey = entity.GetForeignKeys()
            .Single(x => x.PrincipalEntityType.ClrType == typeof(AnalysisRequest));

        Assert.True(requestForeignKey.IsUnique);
        Assert.True(requestForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Cascade, requestForeignKey.DeleteBehavior);
    }

    [Fact]
    public void InsightMapping_PreservesAUniquePositionWithinEachCategory()
    {
        using var dbContext = CreateDbContext();
        var entity = dbContext.Model.FindEntityType(typeof(AnalysisInsight));

        Assert.NotNull(entity);

        var index = entity.GetIndexes().Single(x =>
            x.Properties.Select(property => property.Name).SequenceEqual(
            [
                nameof(AnalysisInsight.AnalysisResultId),
                nameof(AnalysisInsight.Type),
                nameof(AnalysisInsight.Position)
            ]));

        Assert.True(index.IsUnique);

        var resultForeignKey = entity.GetForeignKeys()
            .Single(x => x.PrincipalEntityType.ClrType == typeof(AnalysisResult));

        Assert.Equal(DeleteBehavior.Cascade, resultForeignKey.DeleteBehavior);
    }

    [Fact]
    public void EvidenceMapping_RequiresRealCollectedItemAndPreventsDuplicates()
    {
        using var dbContext = CreateDbContext();
        var entity = dbContext.Model.FindEntityType(typeof(AnalysisEvidence));

        Assert.NotNull(entity);

        var collectedItemForeignKey = entity.GetForeignKeys()
            .Single(x => x.PrincipalEntityType.ClrType == typeof(CollectedMarketItem));
        Assert.Equal(DeleteBehavior.Restrict, collectedItemForeignKey.DeleteBehavior);

        var insightForeignKey = entity.GetForeignKeys()
            .Single(x => x.PrincipalEntityType.ClrType == typeof(AnalysisInsight));
        Assert.Equal(DeleteBehavior.Cascade, insightForeignKey.DeleteBehavior);

        var uniqueIndex = entity.GetIndexes().Single(x =>
            x.Properties.Select(property => property.Name).SequenceEqual(
            [
                nameof(AnalysisEvidence.AnalysisInsightId),
                nameof(AnalysisEvidence.CollectedMarketItemId)
            ]));

        Assert.True(uniqueIndex.IsUnique);
    }

    [Fact]
    public void ProviderCreateScript_ContainsEvidenceForeignKeyAndChecks()
    {
        using var dbContext = CreateDbContext();

        var script = dbContext.Database.GenerateCreateScript();

        Assert.Contains("CK_AnalysisResults_MarketScore_Range", script);
        Assert.Contains("CK_AnalysisResults_SignalStrength_Range", script);
        Assert.Contains("CK_AnalysisInsights_Position_NonNegative", script);
        Assert.Contains("CK_AnalysisInsights_Type_Range", script);
        Assert.Contains("FK_AnalysisEvidences_CollectedMarketItems_CollectedMarketItemId", script);
        Assert.Contains("IX_AnalysisResults_AnalysisRequestId", script);
        Assert.Contains("UNIQUE", script);
        Assert.Contains("ON DELETE RESTRICT", script);
    }
}
