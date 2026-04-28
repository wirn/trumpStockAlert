namespace TrumpStockAlert.Api.Services;

public sealed class CollectorRunResult
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    public required int FetchedPosts { get; init; }

    public required int SavedPosts { get; init; }

    public required int SkippedPosts { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}
