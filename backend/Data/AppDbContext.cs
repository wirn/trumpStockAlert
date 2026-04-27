using Microsoft.EntityFrameworkCore;
using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TruthPost> TruthPosts => Set<TruthPost>();

    public DbSet<PostAnalysis> PostAnalyses => Set<PostAnalysis>();

    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TruthPost>(entity =>
        {
            entity.ToTable("truth_posts");

            entity.HasKey(post => post.Id);

            entity.Property(post => post.Source)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(post => post.Author)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(post => post.ExternalId)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(post => post.Url)
                .IsRequired()
                .HasMaxLength(2048);

            entity.Property(post => post.Content)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            entity.Property(post => post.CreatedAt)
                .IsRequired()
                .HasColumnType("datetimeoffset(7)");

            entity.Property(post => post.CollectedAt)
                .IsRequired()
                .HasColumnType("datetimeoffset(7)");

            entity.Property(post => post.SavedAtUtc)
                .IsRequired()
                .HasColumnType("datetimeoffset(7)");

            entity.Property(post => post.RawJson)
                .HasColumnType("nvarchar(max)");

            entity.HasIndex(post => new { post.Source, post.ExternalId })
                .IsUnique();
        });

        modelBuilder.Entity<PostAnalysis>(entity =>
        {
            entity.ToTable("post_analyses", table =>
            {
                table.HasCheckConstraint(
                    "CK_post_analyses_MarketImpactScore_1_100",
                    "[MarketImpactScore] >= 1 AND [MarketImpactScore] <= 100");
                table.HasCheckConstraint(
                    "CK_post_analyses_Confidence_1_100",
                    "[Confidence] IS NULL OR ([Confidence] >= 1 AND [Confidence] <= 100)");
            });

            entity.HasKey(analysis => analysis.Id);

            entity.Property(analysis => analysis.MarketImpactScore)
                .IsRequired();

            entity.Property(analysis => analysis.Direction)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(analysis => analysis.Reasoning)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            entity.Property(analysis => analysis.AffectedAssetsJson)
                .HasColumnType("nvarchar(max)");

            entity.Property(analysis => analysis.AnalyzerVersion)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(analysis => analysis.RawAiResponse)
                .HasColumnType("nvarchar(max)");

            entity.Property(analysis => analysis.AnalyzedAt)
                .IsRequired()
                .HasColumnType("datetimeoffset(7)");

            entity.Property(analysis => analysis.CreatedAt)
                .IsRequired()
                .HasColumnType("datetimeoffset(7)");

            entity.HasOne(analysis => analysis.Post)
                .WithOne(post => post.Analysis)
                .HasForeignKey<PostAnalysis>(analysis => analysis.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(analysis => analysis.PostId)
                .IsUnique();

        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.ToTable("alerts", table =>
            {
                table.HasCheckConstraint(
                    "CK_alerts_Threshold_1_100",
                    "[Threshold] >= 1 AND [Threshold] <= 100");
            });

            entity.HasKey(alert => alert.Id);

            entity.Property(alert => alert.AlertType)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(alert => alert.Recipient)
                .IsRequired()
                .HasMaxLength(320);

            entity.Property(alert => alert.Subject)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(alert => alert.Body)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            entity.Property(alert => alert.Threshold)
                .IsRequired();

            entity.Property(alert => alert.SentAt)
                .HasColumnType("datetimeoffset(7)");

            entity.Property(alert => alert.SendStatus)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(alert => alert.ErrorMessage)
                .HasColumnType("nvarchar(max)");

            entity.Property(alert => alert.CreatedAt)
                .IsRequired()
                .HasColumnType("datetimeoffset(7)");

            entity.HasOne(alert => alert.Post)
                .WithMany(post => post.Alerts)
                .HasForeignKey(alert => alert.PostId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(alert => alert.PostAnalysis)
                .WithMany(analysis => analysis.Alerts)
                .HasForeignKey(alert => alert.PostAnalysisId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(alert => alert.PostId);
            entity.HasIndex(alert => alert.PostAnalysisId);
            entity.HasIndex(alert => alert.SendStatus);
        });
    }
}
