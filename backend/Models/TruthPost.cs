using System.ComponentModel.DataAnnotations;

namespace TrumpStockAlert.Api.Models;

public sealed class TruthPost
{
    public int Id { get; set; }

    [Required]
    public string Source { get; set; } = string.Empty;

    [Required]
    public string Author { get; set; } = string.Empty;

    [Required]
    public string ExternalId { get; set; } = string.Empty;

    [Required]
    public string Url { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset CollectedAt { get; set; }

    public DateTimeOffset SavedAtUtc { get; set; }

    public string? RawJson { get; set; }

    public PostAnalysis? Analysis { get; set; }

    public ICollection<Alert> Alerts { get; } = new List<Alert>();
}
