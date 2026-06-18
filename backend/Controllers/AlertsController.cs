using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.DTOs;
using TrumpStockAlert.Api.Models;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController(
    AppDbContext dbContext,
    IAlertEvaluator alertEvaluator,
    IOptions<AlertSettings> alertOptions,
    AlertEmailTemplateRenderer emailTemplateRenderer,
    AlertRecipientResolver recipientResolver,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<AlertsController> logger) : ControllerBase
{
    private const string SchedulerKeyHeaderName = "X-TrumpStockAlert-Scheduler-Key";
    private const string SchedulerApiKeyConfigName = "Scheduler:ApiKey";
    private const string EmailPreviewEnabledConfigName = "AlertEmailPreview:Enabled";
    private const string EmailPreviewRecipientConfigName = "AlertEmailPreview:Recipient";
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

    /// <summary>
    /// Sends a protected sample alert email for validating the HTML email layout.
    /// </summary>
    /// <remarks>
    /// Requires the <c>X-TrumpStockAlert-Scheduler-Key</c> header and <c>AlertEmailPreview:Enabled=true</c>.
    /// Does not create alert records.
    /// </remarks>
    [HttpPost("email-preview")]
    [ProducesResponseType(typeof(AlertEmailPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AlertEmailPreviewResponse>> SendEmailPreview(
        [FromHeader(Name = SchedulerKeyHeaderName)] string? schedulerKey,
        CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>(EmailPreviewEnabledConfigName))
        {
            return NotFound();
        }

        if (!AuthorizeRequest(schedulerKey))
        {
            return Unauthorized();
        }

        var startedAt = DateTimeOffset.UtcNow;
        var settings = NormalizeAlertSettings(alertOptions.Value);
        var recipients = ResolvePreviewRecipients(settings);
        var analysis = BuildPreviewAnalysis();
        var sentMessages = new List<AlertEmailMessage>();

        try
        {
            foreach (var recipient in recipients)
            {
                var recipientSettings = settings with { Recipient = recipient };
                var message = emailTemplateRenderer.Render(recipientSettings, analysis);
                logger.LogInformation(
                    "Sending alert email preview. Recipient: {Recipient}. Subject: {Subject}. HtmlBodyPresent: {HtmlBodyPresent}.",
                    message.Recipient,
                    message.Subject,
                    !string.IsNullOrWhiteSpace(message.HtmlBody));

                await emailSender.SendAsync(message, cancellationToken);
                sentMessages.Add(message);
            }

            var finishedAt = DateTimeOffset.UtcNow;
            return Ok(new AlertEmailPreviewResponse
            {
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                DurationMs = (long)(finishedAt - startedAt).TotalMilliseconds,
                Recipient = string.Join(", ", recipients),
                Recipients = recipients,
                Subject = sentMessages.First().Subject,
                HtmlBodyPresent = sentMessages.All(message => !string.IsNullOrWhiteSpace(message.HtmlBody)),
                Message = "Alert email preview sent."
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Alert email preview failed. Recipients: {Recipients}.",
                string.Join(", ", recipients));

            return Problem(
                title: "Alert email preview failed.",
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

    private static AlertSettings NormalizeAlertSettings(AlertSettings settings)
    {
        return new AlertSettings
        {
            Enabled = settings.Enabled,
            Threshold = 70,
            Recipient = settings.Recipient,
            AlertType = string.IsNullOrWhiteSpace(settings.AlertType)
                ? "MarketImpact"
                : settings.AlertType
        };
    }

    private IReadOnlyList<string> ResolvePreviewRecipients(AlertSettings settings)
    {
        var previewRecipient = configuration[EmailPreviewRecipientConfigName]?.Trim();
        if (!string.IsNullOrWhiteSpace(previewRecipient))
        {
            return [previewRecipient];
        }

        return recipientResolver.Resolve(settings);
    }

    private static PostAnalysis BuildPreviewAnalysis()
    {
        var now = DateTimeOffset.UtcNow;
        var postCreatedAt = now.AddMinutes(-6);
        var post = new TruthPost
        {
            Id = 0,
            Source = "truth_social",
            Author = "realDonaldTrump",
            ExternalId = "email-preview-sample",
            Url = "https://truthsocial.com/@realDonaldTrump/posts/email-preview-sample",
            Content = "Iran must not close the Strait of Hormuz. The world needs stable energy prices, and the United States will protect freedom of navigation and keep our markets strong.",
            CreatedAt = postCreatedAt,
            CollectedAt = now,
            SavedAtUtc = now
        };

        return new PostAnalysis
        {
            Id = 0,
            PostId = 0,
            Post = post,
            MarketImpactScore = 84,
            Direction = 18,
            Confidence = 83,
            Reasoning = "Comments linking Iran and the Strait of Hormuz to energy security could lift oil volatility and pressure U.S. equities sensitive to fuel costs, while supporting energy producers.",
            AffectedAssetsJson = """["Crude oil","Energy equities","US equities","USD"]""",
            AnalyzerVersion = "email-preview",
            RawAiResponse = "{}",
            AnalyzedAt = now,
            CreatedAt = now
        };
    }
}
