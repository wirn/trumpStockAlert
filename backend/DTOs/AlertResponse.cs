using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.DTOs;

public sealed class AlertResponse
{
    public required int Id { get; init; }

    public required int PostId { get; init; }

    public required int PostAnalysisId { get; init; }

    public required string AlertType { get; init; }

    public required string Recipient { get; init; }

    public required string Subject { get; init; }

    public required string Body { get; init; }

    public required int Threshold { get; init; }

    public DateTimeOffset? SentAt { get; init; }

    public required string SendStatus { get; init; }

    public string? ErrorMessage { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public static AlertResponse FromEntity(Alert alert)
    {
        return new AlertResponse
        {
            Id = alert.Id,
            PostId = alert.PostId,
            PostAnalysisId = alert.PostAnalysisId,
            AlertType = alert.AlertType,
            Recipient = alert.Recipient,
            Subject = alert.Subject,
            Body = alert.Body,
            Threshold = alert.Threshold,
            SentAt = alert.SentAt,
            SendStatus = alert.SendStatus,
            ErrorMessage = alert.ErrorMessage,
            CreatedAt = alert.CreatedAt
        };
    }
}
