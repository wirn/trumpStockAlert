namespace TrumpStockAlert.Api.DTOs;

public sealed class AnalysisRunResponse
{
    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset FinishedAt { get; init; }

    public required long DurationMs { get; init; }

    public required int AnalyzedCount { get; init; }

    public required int SkippedCount { get; init; }

    public required int ErrorCount { get; init; }

    public required string Message { get; init; }
}
