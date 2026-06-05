namespace TrumpStockAlert.Api.Services;

public sealed class AlertRunResult
{
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
}
