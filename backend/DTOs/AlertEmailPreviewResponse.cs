namespace TrumpStockAlert.Api.DTOs;

public sealed class AlertEmailPreviewResponse
{
    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset FinishedAt { get; init; }

    public required long DurationMs { get; init; }

    public required string Recipient { get; init; }

    public required string Subject { get; init; }

    public required bool HtmlBodyPresent { get; init; }

    public required string Message { get; init; }
}
