namespace TrumpStockAlert.Api.Services;

public sealed class MarketImpactAiResponse
{
    public required int MarketImpactScore { get; init; }

    public required string Direction { get; init; }

    public required string Reasoning { get; init; }

    public required IReadOnlyList<string> AffectedAssets { get; init; }

    public required int Confidence { get; init; }
}
