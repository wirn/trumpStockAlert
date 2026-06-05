using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.DTOs;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController(
    AppDbContext dbContext,
    IAlertEvaluator alertEvaluator,
    IConfiguration configuration,
    ILogger<AlertsController> logger) : ControllerBase
{
    private const string SchedulerKeyHeaderName = "X-TrumpStockAlert-Scheduler-Key";
    private const string SchedulerApiKeyConfigName = "Scheduler:ApiKey";
    private const int DefaultLimit = 50;
    private const int MaxLimit = 500;

    /// <summary>
    /// Runs local alert evaluation for saved analyses and records alert notifications.
    /// </summary>
    /// <remarks>
    /// Requires the <c>X-TrumpStockAlert-Scheduler-Key</c> header.
    /// Safe to call repeatedly; already-created alerts are skipped.
    /// </remarks>
    [HttpPost("run")]
    [ProducesResponseType(typeof(AlertRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AlertRunResponse>> RunAlerts(
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
            var result = await alertEvaluator.RunAsync(cancellationToken);
            var finishedAt = DateTimeOffset.UtcNow;
            return Ok(AlertRunResponse.FromResult(result, startedAt, finishedAt));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Alert run failed.");
            return Problem(
                title: "Alert run failed.",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AlertResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AlertResponse>>> GetLatest(
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = limit ?? DefaultLimit;
        if (effectiveLimit < 1 || effectiveLimit > MaxLimit)
        {
            return BadRequest($"limit must be between 1 and {MaxLimit}.");
        }

        var alerts = await dbContext.Alerts
            .AsNoTracking()
            .OrderByDescending(alert => alert.CreatedAt)
            .Take(effectiveLimit)
            .ToListAsync(cancellationToken);

        return Ok(alerts.Select(AlertResponse.FromEntity).ToList());
    }

    private bool AuthorizeRequest(string? apiKey)
    {
        var configuredKey = configuration[SchedulerApiKeyConfigName];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            logger.LogError(
                "Alert run rejected: {ConfigName} is not configured.",
                SchedulerApiKeyConfigName);
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Alert run rejected: scheduler key header is missing.");
            return false;
        }

        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var providedBytes = Encoding.UTF8.GetBytes(apiKey);
        return configuredBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
    }
}
