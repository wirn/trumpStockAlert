using System.ComponentModel.DataAnnotations;

namespace TrumpStockAlert.Api.DTOs;

public sealed class ReportCollectorRunRequest
{
    [Required]
    public DateTimeOffset StartedAt { get; init; }

    [Required]
    public DateTimeOffset FinishedAt { get; init; }

    public string TriggerType { get; init; } = "scheduler";

    public bool Success { get; init; }

    public int FetchedCount { get; init; }

    public int InsertedCount { get; init; }

    public int DuplicateCount { get; init; }

    public int ErrorCount { get; init; }

    public string Message { get; init; } = string.Empty;
}
