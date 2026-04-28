namespace TrumpStockAlert.Api.Services;

public sealed class CollectorProcessRunResult
{
    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset FinishedAt { get; init; }

    public required int ExitCode { get; init; }

    public required bool Success { get; init; }

    public required bool TimedOut { get; init; }

    public int? FetchedPosts { get; init; }

    public int? SavedPosts { get; init; }

    public int? SkippedPosts { get; init; }

    public required string Message { get; init; }

    public required string Stdout { get; init; }

    public required string Stderr { get; init; }
}
