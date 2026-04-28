namespace TrumpStockAlert.Api.DTOs;

public sealed class CollectorTestRunResponse
{
    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset FinishedAt { get; init; }

    public required int ExitCode { get; init; }

    public required bool Success { get; init; }

    public required bool TimedOut { get; init; }

    public required string Stdout { get; init; }

    public required string Stderr { get; init; }
}
