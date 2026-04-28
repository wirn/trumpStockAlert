using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.DTOs;

public sealed class CollectorRunTestResponse
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    public int? FetchedPosts { get; init; }

    public int? SavedPosts { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required int ExitCode { get; init; }

    public required bool TimedOut { get; init; }

    public required string Stdout { get; init; }

    public required string Stderr { get; init; }

    public static CollectorRunTestResponse FromResult(CollectorTestRunResult result)
    {
        return new CollectorRunTestResponse
        {
            Success = result.Success,
            Message = result.Message,
            FetchedPosts = result.FetchedPosts,
            SavedPosts = result.SavedPosts,
            Timestamp = result.FinishedAt,
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut,
            Stdout = result.Stdout,
            Stderr = result.Stderr
        };
    }
}
