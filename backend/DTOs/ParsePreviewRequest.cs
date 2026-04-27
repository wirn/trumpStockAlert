using System.ComponentModel.DataAnnotations;

namespace TrumpStockAlert.Api.DTOs;

public sealed class ParsePreviewRequest
{
    [Required]
    public string RawJson { get; init; } = string.Empty;
}
