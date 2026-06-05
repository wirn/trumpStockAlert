using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.DTOs;

public sealed class AlertRunResponse
{
    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset FinishedAt { get; init; }

    public required long DurationMs { get; init; }

    public required int EvaluatedAnalysisCount { get; init; }

    public required int EligibleAnalysisCount { get; init; }

    public required int BelowThresholdCount { get; init; }

    public required int DuplicateCount { get; init; }

    public required int CreatedAlertCount { get; init; }

    public required int SentCount { get; init; }

    public required int FailedCount { get; init; }

    public required int Threshold { get; init; }

    public required string Recipient { get; init; }

    public required string Message { get; init; }

    public required IReadOnlyList<int> CreatedAlertIds { get; init; }

    public static AlertRunResponse FromResult(
        AlertRunResult result,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt)
    {
        return new AlertRunResponse
        {
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            DurationMs = (long)(finishedAt - startedAt).TotalMilliseconds,
            EvaluatedAnalysisCount = result.EvaluatedAnalysisCount,
            EligibleAnalysisCount = result.EligibleAnalysisCount,
            BelowThresholdCount = result.BelowThresholdCount,
            DuplicateCount = result.DuplicateCount,
            CreatedAlertCount = result.CreatedAlertCount,
            SentCount = result.SentCount,
            FailedCount = result.FailedCount,
            Threshold = result.Threshold,
            Recipient = result.Recipient,
            Message = result.Message,
            CreatedAlertIds = result.CreatedAlertIds
        };
    }
}
