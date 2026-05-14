using System.ComponentModel.DataAnnotations;

namespace TrumpStockAlert.Api.Models;

public sealed class FetcherRun
{
    public int Id { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public long DurationMs { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;

    [Required]
    public string TriggerType { get; set; } = string.Empty;

    public int FetchedCount { get; set; }

    public int InsertedCount { get; set; }

    public int DuplicateCount { get; set; }

    public int ErrorCount { get; set; }

    [Required]
    public string Message { get; set; } = string.Empty;
}
