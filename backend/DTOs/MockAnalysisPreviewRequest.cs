using System.ComponentModel.DataAnnotations;

namespace TrumpStockAlert.Api.DTOs;

public sealed class MockAnalysisPreviewRequest
{
    [Required]
    public string Content { get; init; } = string.Empty;
}
