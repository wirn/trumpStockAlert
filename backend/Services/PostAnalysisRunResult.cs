namespace TrumpStockAlert.Api.Services;

public sealed class PostAnalysisRunResult
{
    public required int AnalyzedCount { get; init; }

    public required int SkippedCount { get; init; }

    public required int FailedCount { get; init; }

    public required string Message { get; init; }

    public required IReadOnlyList<int> AnalyzedPostIds { get; init; }

    public required IReadOnlyList<int> FailedPostIds { get; init; }
}
