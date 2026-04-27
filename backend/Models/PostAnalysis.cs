using System.ComponentModel.DataAnnotations;

namespace TrumpStockAlert.Api.Models;

public sealed class PostAnalysis
{
    public int Id { get; set; }

    public int PostId { get; set; }

    public TruthPost Post { get; set; } = null!;

    public int MarketImpactScore { get; set; }

    [Required]
    public string Direction { get; set; } = string.Empty;

    [Required]
    public string Reasoning { get; set; } = string.Empty;

    public string? AffectedAssetsJson { get; set; }

    public int? Confidence { get; set; }

    [Required]
    public string AnalyzerVersion { get; set; } = string.Empty;

    public string? RawAiResponse { get; set; }

    public DateTimeOffset AnalyzedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Alert> Alerts { get; } = new List<Alert>();
}
