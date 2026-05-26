using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public sealed class PostAnalysisRunner(
    AppDbContext dbContext,
    IMarketImpactAnalyzer analyzer,
    ILogger<PostAnalysisRunner> logger) : IPostAnalysisRunner
{
    private const string NoTextContentPlaceholder = "[No text content]";

    public async Task<PostAnalysisRunResult> AnalyzePendingPostsAsync(
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Post analysis run starting.");

        var totalPostCount = await dbContext.TruthPosts.CountAsync(cancellationToken);
        var pendingPosts = await dbContext.TruthPosts
            .AsNoTracking()
            .Where(post => !dbContext.PostAnalyses.Any(analysis => analysis.PostId == post.Id))
            .OrderBy(post => post.CreatedAt)
            .ToListAsync(cancellationToken);

        var skippedAlreadyAnalyzedCount = totalPostCount - pendingPosts.Count;
        var skippedNoTextContentCount = 0;
        logger.LogInformation(
            "Found {PendingCount} unanalyzed posts for analysis. {SkippedAlreadyAnalyzedCount} posts already have analysis.",
            pendingPosts.Count,
            skippedAlreadyAnalyzedCount);

        if (pendingPosts.Count == 0)
        {
            logger.LogInformation(
                "Post analysis run completed with no unanalyzed posts. AnalyzedCount=0 SkippedNoTextContentCount=0 SkippedAlreadyAnalyzedCount={SkippedAlreadyAnalyzedCount} FailedCount=0",
                skippedAlreadyAnalyzedCount);
            return new PostAnalysisRunResult
            {
                AnalyzedCount = 0,
                SkippedCount = skippedAlreadyAnalyzedCount,
                SkippedAlreadyAnalyzedCount = skippedAlreadyAnalyzedCount,
                SkippedNoTextContentCount = 0,
                FailedCount = 0,
                Message = "No pending posts found for analysis.",
                AnalyzedPostIds = [],
                FailedPostIds = []
            };
        }

        var analyzedPostIds = new List<int>();
        var failedPostIds = new List<int>();

        foreach (var post in pendingPosts)
        {
            PostAnalysis? analysis = null;

            try
            {
                if (!IsAnalyzableContent(post.Content))
                {
                    skippedNoTextContentCount++;
                    logger.LogInformation(
                        "Skipping truth post {PostId} ({ExternalId}) because it has missing/no text content.",
                        post.Id,
                        post.ExternalId);
                    continue;
                }

                logger.LogInformation(
                    "Analyzing truth post {PostId} ({ExternalId}).",
                    post.Id,
                    post.ExternalId);

                var result = await analyzer.AnalyzeAsync(post, cancellationToken);
                var now = DateTimeOffset.UtcNow;

                analysis = new PostAnalysis
                {
                    PostId = post.Id,
                    MarketImpactScore = result.MarketImpactScore,
                    Direction = result.Direction,
                    Reasoning = result.Reasoning,
                    AffectedAssetsJson = JsonSerializer.Serialize(result.AffectedAssets),
                    Confidence = result.ConfidenceScore,
                    AnalyzerVersion = result.AnalyzerVersion,
                    RawAiResponse = JsonResponseText.NormalizeObjectJson(result.RawAiResponse),
                    AnalyzedAt = now,
                    CreatedAt = now
                };

                dbContext.PostAnalyses.Add(analysis);
                await dbContext.SaveChangesAsync(cancellationToken);

                analyzedPostIds.Add(post.Id);
                logger.LogInformation(
                    "Saved analysis {AnalysisId} for truth post {PostId} ({ExternalId}).",
                    analysis.Id,
                    post.Id,
                    post.ExternalId);
            }
            catch (DbUpdateException exception)
            {
                if (analysis is not null)
                {
                    dbContext.Entry(analysis).State = EntityState.Detached;
                }

                var alreadyAnalyzed = await dbContext.PostAnalyses
                    .AsNoTracking()
                    .AnyAsync(existing => existing.PostId == post.Id, cancellationToken);

                if (alreadyAnalyzed)
                {
                    skippedAlreadyAnalyzedCount++;
                    logger.LogWarning(
                        exception,
                        "Truth post {PostId} ({ExternalId}) was analyzed concurrently; skipping duplicate analysis.",
                        post.Id,
                        post.ExternalId);
                    continue;
                }

                failedPostIds.Add(post.Id);
                logger.LogError(
                    exception,
                    "Failed to save analysis for truth post {PostId} ({ExternalId}).",
                    post.Id,
                    post.ExternalId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failedPostIds.Add(post.Id);
                logger.LogError(
                    exception,
                    "Failed to analyze truth post {PostId} ({ExternalId}).",
                    post.Id,
                    post.ExternalId);
            }
        }

        var skippedCount = skippedAlreadyAnalyzedCount + skippedNoTextContentCount;
        var message = $"Analyzed {analyzedPostIds.Count} posts, skipped {skippedCount}, failed {failedPostIds.Count}.";
        logger.LogInformation(
            "Post analysis run completed. AnalyzedCount={AnalyzedCount} SkippedCount={SkippedCount} SkippedNoTextContentCount={SkippedNoTextContentCount} SkippedAlreadyAnalyzedCount={SkippedAlreadyAnalyzedCount} FailedCount={FailedCount}. Message={Message}",
            analyzedPostIds.Count,
            skippedCount,
            skippedNoTextContentCount,
            skippedAlreadyAnalyzedCount,
            failedPostIds.Count,
            message);

        return new PostAnalysisRunResult
        {
            AnalyzedCount = analyzedPostIds.Count,
            SkippedCount = skippedCount,
            SkippedAlreadyAnalyzedCount = skippedAlreadyAnalyzedCount,
            SkippedNoTextContentCount = skippedNoTextContentCount,
            FailedCount = failedPostIds.Count,
            Message = message,
            AnalyzedPostIds = analyzedPostIds,
            FailedPostIds = failedPostIds
        };
    }

    public static bool IsAnalyzableContent(string? content)
    {
        return !string.Equals(
            content,
            NoTextContentPlaceholder,
            StringComparison.Ordinal);
    }

}
