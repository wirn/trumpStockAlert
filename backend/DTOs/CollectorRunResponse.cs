using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.DTOs;

public sealed class CollectorRunResponse
{
    public required string Status { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset FinishedAt { get; init; }

    public required long DurationMs { get; init; }

    public required int FetchedCount { get; init; }

    public required int InsertedCount { get; init; }

    public required int DuplicateCount { get; init; }

    public required int ErrorCount { get; init; }

    public required string Message { get; init; }

    public required bool Success { get; init; }

    public required int FetchedPosts { get; init; }

    public required int SavedPosts { get; init; }

    public required int SkippedPosts { get; init; }

    public required int FailedPosts { get; init; }

    public static CollectorRunResponse FromResult(CollectorRunResult result)
    {
        return new CollectorRunResponse
        {
            Status = result.Success ? "completed" : "failed",
            StartedAt = result.StartedAt,
            FinishedAt = result.FinishedAt,
            DurationMs = result.DurationMs,
            FetchedCount = result.FetchedPosts,
            InsertedCount = result.SavedPosts,
            DuplicateCount = result.SkippedPosts,
            ErrorCount = result.FailedPosts,
            Message = result.Message,
            Success = result.Success,
            FetchedPosts = result.FetchedPosts,
            SavedPosts = result.SavedPosts,
            SkippedPosts = result.SkippedPosts,
            FailedPosts = result.FailedPosts
        };
    }
}
