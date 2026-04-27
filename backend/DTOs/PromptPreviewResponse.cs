using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.DTOs;

public sealed class PromptPreviewResponse
{
    public required string Prompt { get; init; }

    public required MarketImpactAiResponse ExampleResponse { get; init; }
}
