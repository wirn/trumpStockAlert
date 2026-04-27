using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.DTOs;

public sealed class ParsePreviewResponse
{
    public required bool IsValid { get; init; }

    public MarketImpactAiResponse? ParsedResponse { get; init; }

    public string? Error { get; init; }
}
