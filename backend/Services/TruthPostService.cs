using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.DTOs;
using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public sealed class TruthPostService(
    AppDbContext dbContext,
    ILogger<TruthPostService> logger) : ITruthPostService
{
    public async Task<TruthPostSaveResult> SaveAsync(
        CreateTruthPostRequest request,
        CancellationToken cancellationToken)
    {
        var source = request.Source.Trim();
        var externalId = request.ExternalId.Trim();

        var existing = await FindByExternalKeyAsync(source, externalId, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation(
                "Truth post already exists for {Source}/{ExternalId}; returning existing row {Id}.",
                source,
                externalId,
                existing.Id);
            return new TruthPostSaveResult(existing, Created: false);
        }

        var post = new TruthPost
        {
            Source = source,
            Author = request.Author.Trim(),
            ExternalId = externalId,
            Url = request.Url.Trim(),
            Content = request.Content.Trim(),
            CreatedAt = request.CreatedAt.ToUniversalTime(),
            CollectedAt = request.CollectedAt.ToUniversalTime(),
            SavedAtUtc = DateTimeOffset.UtcNow,
            RawJson = request.Raw.HasValue
                ? JsonSerializer.Serialize(request.Raw.Value)
                : null
        };

        dbContext.TruthPosts.Add(post);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Saved truth post {Id} for {Source}/{ExternalId}.",
                post.Id,
                post.Source,
                post.ExternalId);
            return new TruthPostSaveResult(post, Created: true);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(post).State = EntityState.Detached;

            var duplicate = await FindByExternalKeyAsync(source, externalId, cancellationToken);
            if (duplicate is not null)
            {
                logger.LogInformation(
                    "Truth post {Source}/{ExternalId} was inserted concurrently; returning existing row {Id}.",
                    source,
                    externalId,
                    duplicate.Id);
                return new TruthPostSaveResult(duplicate, Created: false);
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<TruthPost>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        return await dbContext.TruthPosts
            .AsNoTracking()
            .Include(post => post.Analysis)
            .OrderByDescending(post => post.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<TruthPost?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return dbContext.TruthPosts
            .AsNoTracking()
            .Include(post => post.Analysis)
            .FirstOrDefaultAsync(post => post.Id == id, cancellationToken);
    }

    private Task<TruthPost?> FindByExternalKeyAsync(
        string source,
        string externalId,
        CancellationToken cancellationToken)
    {
        return dbContext.TruthPosts
            .Include(post => post.Analysis)
            .FirstOrDefaultAsync(
                post => post.Source == source && post.ExternalId == externalId,
                cancellationToken);
    }
}
