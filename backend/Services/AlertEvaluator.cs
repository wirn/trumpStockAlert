using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public sealed class AlertEvaluator(
    AppDbContext dbContext,
    IOptions<AlertSettings> options,
    IEmailSender emailSender,
    ILogger<AlertEvaluator> logger) : IAlertEvaluator
{
    private const string SentStatus = "Sent";
    private const string FailedStatus = "Failed";

    public async Task<AlertRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var settings = NormalizeSettings(options.Value);
        if (!settings.Enabled)
        {
            return new AlertRunResult
            {
                EvaluatedAnalysisCount = 0,
                EligibleAnalysisCount = 0,
                BelowThresholdCount = 0,
                DuplicateCount = 0,
                CreatedAlertCount = 0,
                SentCount = 0,
                FailedCount = 0,
                Threshold = settings.Threshold,
                Recipient = settings.Recipient,
                Message = "Alerts are disabled.",
                CreatedAlertIds = []
            };
        }

        var analyses = await dbContext.PostAnalyses
            .Include(analysis => analysis.Post)
            .OrderBy(analysis => analysis.CreatedAt)
            .ToListAsync(cancellationToken);

        var belowThresholdCount = 0;
        var duplicateCount = 0;
        var sentCount = 0;
        var failedCount = 0;
        var createdAlertIds = new List<int>();

        foreach (var analysis in analyses)
        {
            if (analysis.MarketImpactScore < settings.Threshold)
            {
                belowThresholdCount++;
                continue;
            }

            var isDuplicate = await dbContext.Alerts
                .AsNoTracking()
                .AnyAsync(alert =>
                    alert.PostAnalysisId == analysis.Id
                    && alert.AlertType == settings.AlertType
                    && alert.Recipient == settings.Recipient,
                    cancellationToken);

            if (isDuplicate)
            {
                duplicateCount++;
                continue;
            }

            var message = BuildMessage(settings, analysis);
            var alert = new Alert
            {
                PostId = analysis.PostId,
                PostAnalysisId = analysis.Id,
                AlertType = settings.AlertType,
                Recipient = settings.Recipient,
                Subject = message.Subject,
                Body = message.Body,
                Threshold = settings.Threshold,
                SendStatus = SentStatus,
                CreatedAt = DateTimeOffset.UtcNow
            };

            try
            {
                await emailSender.SendAsync(message, cancellationToken);
                alert.SentAt = DateTimeOffset.UtcNow;
                sentCount++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                alert.SendStatus = FailedStatus;
                alert.ErrorMessage = exception.Message;
                failedCount++;
                logger.LogError(
                    exception,
                    "Failed to send alert for analysis {AnalysisId}.",
                    analysis.Id);
            }

            dbContext.Alerts.Add(alert);
            await dbContext.SaveChangesAsync(cancellationToken);
            createdAlertIds.Add(alert.Id);
        }

        var eligibleAnalysisCount = analyses.Count - belowThresholdCount;
        var messageText = $"Created {createdAlertIds.Count} alerts, sent {sentCount}, skipped {belowThresholdCount} below threshold and {duplicateCount} duplicates.";
        logger.LogInformation(
            "Alert run completed. Evaluated: {EvaluatedAnalysisCount}. Eligible: {EligibleAnalysisCount}. Created: {CreatedAlertCount}. Sent: {SentCount}. Failed: {FailedCount}. Duplicates: {DuplicateCount}. Threshold: {Threshold}.",
            analyses.Count,
            eligibleAnalysisCount,
            createdAlertIds.Count,
            sentCount,
            failedCount,
            duplicateCount,
            settings.Threshold);

        return new AlertRunResult
        {
            EvaluatedAnalysisCount = analyses.Count,
            EligibleAnalysisCount = eligibleAnalysisCount,
            BelowThresholdCount = belowThresholdCount,
            DuplicateCount = duplicateCount,
            CreatedAlertCount = createdAlertIds.Count,
            SentCount = sentCount,
            FailedCount = failedCount,
            Threshold = settings.Threshold,
            Recipient = settings.Recipient,
            Message = messageText,
            CreatedAlertIds = createdAlertIds
        };
    }

    private static AlertEmailMessage BuildMessage(AlertSettings settings, PostAnalysis analysis)
    {
        var subject = $"Market impact alert: score {analysis.MarketImpactScore}";
        var body = string.Join(
            Environment.NewLine,
            $"A Truth Social post met the alert threshold of {settings.Threshold}.",
            $"Post ID: {analysis.PostId}",
            $"Analysis ID: {analysis.Id}",
            $"Score: {analysis.MarketImpactScore}",
            $"Direction: {analysis.Direction}",
            $"Confidence: {analysis.Confidence}",
            $"URL: {analysis.Post.Url}",
            string.Empty,
            analysis.Reasoning);

        return new AlertEmailMessage
        {
            Recipient = settings.Recipient,
            Subject = subject,
            Body = body
        };
    }

    private static AlertSettings NormalizeSettings(AlertSettings settings)
    {
        return new AlertSettings
        {
            Enabled = settings.Enabled,
            Threshold = Math.Clamp(settings.Threshold, 1, 100),
            Recipient = string.IsNullOrWhiteSpace(settings.Recipient)
                ? "log-only@trumpstockalert.local"
                : settings.Recipient,
            AlertType = string.IsNullOrWhiteSpace(settings.AlertType)
                ? "MarketImpact"
                : settings.AlertType
        };
    }
}
