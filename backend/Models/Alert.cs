using System.ComponentModel.DataAnnotations;

namespace TrumpStockAlert.Api.Models;

public sealed class Alert
{
    public int Id { get; set; }

    public int PostId { get; set; }

    public TruthPost Post { get; set; } = null!;

    public int PostAnalysisId { get; set; }

    public PostAnalysis PostAnalysis { get; set; } = null!;

    [Required]
    public string AlertType { get; set; } = string.Empty;

    [Required]
    public string Recipient { get; set; } = string.Empty;

    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public int Threshold { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    [Required]
    public string SendStatus { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
