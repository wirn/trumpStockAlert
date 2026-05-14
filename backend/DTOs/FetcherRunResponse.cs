using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.DTOs;

public sealed class FetcherRunResponse
{
    public required int Id { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset FinishedAt { get; init; }

    public required long DurationMs { get; init; }

    public required string Status { get; init; }

    public required string TriggerType { get; init; }

    public required int FetchedCount { get; init; }

    public required int InsertedCount { get; init; }

    public required int DuplicateCount { get; init; }

    public required int ErrorCount { get; init; }

    public required string Message { get; init; }

    public static FetcherRunResponse FromEntity(FetcherRun run)
    {
        return new FetcherRunResponse
        {
            Id = run.Id,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt,
            DurationMs = run.DurationMs,
            Status = run.Status,
            TriggerType = run.TriggerType,
            FetchedCount = run.FetchedCount,
            InsertedCount = run.InsertedCount,
            DuplicateCount = run.DuplicateCount,
            ErrorCount = run.ErrorCount,
            Message = run.Message
        };
    }
}
