using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace TrumpStockAlert.Api.DTOs;

public sealed class CreateTruthPostRequest
{
    [Required]
    public string Source { get; init; } = string.Empty;

    [Required]
    public string Author { get; init; } = string.Empty;

    [Required]
    public string ExternalId { get; init; } = string.Empty;

    [Required]
    public string Url { get; init; } = string.Empty;

    [Required]
    public string Content { get; init; } = string.Empty;

    [Required]
    public DateTimeOffset CreatedAt { get; init; }

    [Required]
    public DateTimeOffset CollectedAt { get; init; }

    public JsonElement? Raw { get; init; }
}
