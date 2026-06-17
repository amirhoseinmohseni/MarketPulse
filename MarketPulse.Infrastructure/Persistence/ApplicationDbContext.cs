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
    public DbSet<SearchQuery> SearchQueries { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AnalysisRequest>()
            .HasOne(r => r.Result)
            .WithOne(res => res.Request)
            .HasForeignKey<AnalysisResult>(res => res.AnalysisRequestId);

        modelBuilder.Entity<SearchQuery>()
                .HasOne(x => x.Request)
                .WithMany(x => x.SearchQueries)
                .HasForeignKey(x => x.AnalysisRequestId);

    }
}
