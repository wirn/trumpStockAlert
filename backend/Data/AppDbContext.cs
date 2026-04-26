using Microsoft.EntityFrameworkCore;
using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TruthPost> TruthPosts => Set<TruthPost>();

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
    }
}
