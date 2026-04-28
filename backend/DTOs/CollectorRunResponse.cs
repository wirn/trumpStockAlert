using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.DTOs;

public sealed class CollectorRunResponse
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    public required int FetchedPosts { get; init; }

    public required int SavedPosts { get; init; }

    public required int SkippedPosts { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public static CollectorRunResponse FromResult(CollectorRunResult result)
    {
        return new CollectorRunResponse
        {
            Success = result.Success,
            Message = result.Message,
            FetchedPosts = result.FetchedPosts,
            SavedPosts = result.SavedPosts,
            SkippedPosts = result.SkippedPosts,
            Timestamp = result.Timestamp
        };
    }
}
