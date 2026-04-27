using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.DTOs;

public sealed class OpenAiAnalysisPreviewResponse
{
    public required MarketImpactAnalysisResult Analysis { get; init; }

    public required string RawAiResponse { get; init; }
}
