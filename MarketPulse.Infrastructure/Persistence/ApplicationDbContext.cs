using MarketPulse.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketPulse.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AnalysisRequest> AnalysisRequests { get; set; }
    public DbSet<AnalysisResult> AnalysisResults { get; set; }
    public DbSet<AnalysisInsight> AnalysisInsights { get; set; }
    public DbSet<AnalysisEvidence> AnalysisEvidences { get; set; }
    public DbSet<SearchQuery> SearchQueries { get; set; }
    public DbSet<RedditPost> RedditPosts { get; set; }
    public DbSet<CollectedMarketItem> CollectedMarketItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AnalysisRequest>()
            .HasOne(r => r.Result)
            .WithOne(res => res.Request)
            .HasForeignKey<AnalysisResult>(res => res.AnalysisRequestId);

        modelBuilder.Entity<AnalysisResult>()
            .ToTable("AnalysisResults", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_AnalysisResults_MarketScore_Range",
                    "\"MarketScore\" IS NULL OR (\"MarketScore\" >= 0 AND \"MarketScore\" <= 100)");
                tableBuilder.HasCheckConstraint(
                    "CK_AnalysisResults_SignalStrength_Range",
                    "\"SignalStrength\" >= 0 AND \"SignalStrength\" <= 2");
            });

        modelBuilder.Entity<AnalysisResult>()
            .Property(x => x.Summary)
            .IsRequired();

        modelBuilder.Entity<AnalysisInsight>()
            .HasOne(x => x.AnalysisResult)
            .WithMany(x => x.Insights)
            .HasForeignKey(x => x.AnalysisResultId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AnalysisInsight>()
            .Property(x => x.Text)
            .IsRequired();

        modelBuilder.Entity<AnalysisInsight>()
            .ToTable("AnalysisInsights", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_AnalysisInsights_Type_Range",
                    "\"Type\" >= 0 AND \"Type\" <= 3");
                tableBuilder.HasCheckConstraint(
                    "CK_AnalysisInsights_Position_NonNegative",
                    "\"Position\" >= 0");
            });

        modelBuilder.Entity<AnalysisInsight>()
            .HasIndex(x => new { x.AnalysisResultId, x.Type, x.Position })
            .IsUnique();

        modelBuilder.Entity<AnalysisEvidence>()
            .HasOne(x => x.AnalysisInsight)
            .WithMany(x => x.Evidence)
            .HasForeignKey(x => x.AnalysisInsightId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AnalysisEvidence>()
            .HasOne(x => x.CollectedMarketItem)
            .WithMany(x => x.AnalysisEvidence)
            .HasForeignKey(x => x.CollectedMarketItemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AnalysisEvidence>()
            .HasIndex(x => new { x.AnalysisInsightId, x.CollectedMarketItemId })
            .IsUnique();

        modelBuilder.Entity<SearchQuery>()
            .HasOne(x => x.Request)
            .WithMany(x => x.SearchQueries)
            .HasForeignKey(x => x.AnalysisRequestId);

        modelBuilder.Entity<RedditPost>()
            .HasOne(x => x.Request)
            .WithMany(x => x.RedditPosts)
            .HasForeignKey(x => x.AnalysisRequestId);

        modelBuilder.Entity<RedditPost>()
            .HasOne(x => x.SearchQuery)
            .WithMany()
            .HasForeignKey(x => x.SearchQueryId);

        modelBuilder.Entity<RedditPost>()
            .HasIndex(x => new { x.AnalysisRequestId, x.RedditPostId })
            .IsUnique();

        modelBuilder.Entity<CollectedMarketItem>()
            .HasOne(x => x.Request)
            .WithMany(x => x.CollectedMarketItems)
            .HasForeignKey(x => x.AnalysisRequestId);

        modelBuilder.Entity<CollectedMarketItem>()
            .HasOne(x => x.SearchQuery)
            .WithMany()
            .HasForeignKey(x => x.SearchQueryId);

        modelBuilder.Entity<CollectedMarketItem>()
            .HasIndex(x => new { x.AnalysisRequestId, x.Source, x.ExternalId })
            .IsUnique();
    }
}
