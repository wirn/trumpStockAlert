using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.DTOs;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Controllers;

[ApiController]
[Route("api/analyses")]
public sealed class AnalysesController(
    AppDbContext dbContext,
    IPostAnalysisRunner analysisRunner,
    IConfiguration configuration,
    ILogger<AnalysesController> logger) : ControllerBase
{
    private const string SchedulerKeyHeaderName = "X-TrumpStockAlert-Scheduler-Key";
    private const string SchedulerApiKeyConfigName = "Scheduler:ApiKey";
    private const int DefaultLimit = 50;
    private const int MaxLimit = 500;

    /// <summary>
    /// Runs market-impact analysis for all unanalyzed TruthPosts and saves results.
    /// </summary>
    /// <remarks>
    /// Requires the <c>X-TrumpStockAlert-Scheduler-Key</c> header.
    /// Safe to call repeatedly; already-analyzed posts are skipped.
    /// </remarks>
    [HttpPost("run")]
    [ProducesResponseType(typeof(AnalysisRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AnalysisRunResponse>> RunAnalysis(
        [FromHeader(Name = SchedulerKeyHeaderName)] string? schedulerKey,
        CancellationToken cancellationToken)
    {
        if (!AuthorizeRequest(schedulerKey))
        {
            return Unauthorized();
        }

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            var result = await analysisRunner.AnalyzePendingPostsAsync(cancellationToken);
            var finishedAt = DateTimeOffset.UtcNow;

            logger.LogInformation(
                "Analysis run completed. Analyzed: {AnalyzedCount}. Skipped: {SkippedCount}. Errors: {ErrorCount}.",
                result.AnalyzedCount,
                result.SkippedCount,
                result.FailedCount);

            return Ok(new AnalysisRunResponse
            {
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                DurationMs = (long)(finishedAt - startedAt).TotalMilliseconds,
                AnalyzedCount = result.AnalyzedCount,
                SkippedCount = result.SkippedCount,
                ErrorCount = result.FailedCount,
                Message = result.Message,
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Analysis run failed.");
            return Problem(
                title: "Analysis run failed.",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Returns recent analyses with enriched post data.
    /// </summary>
    [HttpGet("latest")]
    [ProducesResponseType(typeof(IReadOnlyList<PostAnalysisDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PostAnalysisDetailResponse>>> GetLatest(
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = limit ?? DefaultLimit;
        if (effectiveLimit < 1 || effectiveLimit > MaxLimit)
        {
            return BadRequest($"limit must be between 1 and {MaxLimit}.");
        }

        var analyses = await dbContext.PostAnalyses
            .AsNoTracking()
            .Include(a => a.Post)
            .OrderByDescending(a => a.CreatedAt)
            .Take(effectiveLimit)
            .ToListAsync(cancellationToken);

        return Ok(analyses.Select(PostAnalysisDetailResponse.FromEntity).ToList());
    }

    /// <summary>
    /// Returns recent analyses.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PostAnalysisResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PostAnalysisResponse>>> GetAnalyses(
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = limit ?? DefaultLimit;
        if (effectiveLimit < 1 || effectiveLimit > MaxLimit)
        {
            return BadRequest($"limit must be between 1 and {MaxLimit}.");
        }

        var analyses = await dbContext.PostAnalyses
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(effectiveLimit)
            .ToListAsync(cancellationToken);

        return Ok(analyses.Select(PostAnalysisResponse.FromEntity).ToList());
    }

    private bool AuthorizeRequest(string? apiKey)
    {
        var configuredKey = configuration[SchedulerApiKeyConfigName];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            logger.LogError(
                "Analysis run rejected: {ConfigName} is not configured.",
                SchedulerApiKeyConfigName);
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Analysis run rejected: scheduler key header is missing.");
            return false;
        }

        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var providedBytes = Encoding.UTF8.GetBytes(apiKey);
        return configuredBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
    }
}
