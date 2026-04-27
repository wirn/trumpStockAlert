using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.DTOs;

public sealed class PostAnalysisResponse
{
    public required int Id { get; init; }

    public required int PostId { get; init; }

    public required int MarketImpactScore { get; init; }

    public required string Direction { get; init; }

    public required string Reasoning { get; init; }

    public string? AffectedAssetsJson { get; init; }

    public int? Confidence { get; init; }

    public required string AnalyzerVersion { get; init; }

    public string? RawAiResponse { get; init; }

    public required DateTimeOffset AnalyzedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public static PostAnalysisResponse FromEntity(PostAnalysis analysis)
    {
        return new PostAnalysisResponse
        {
            Id = analysis.Id,
            PostId = analysis.PostId,
            MarketImpactScore = analysis.MarketImpactScore,
            Direction = analysis.Direction,
            Reasoning = analysis.Reasoning,
            AffectedAssetsJson = analysis.AffectedAssetsJson,
            Confidence = analysis.Confidence,
            AnalyzerVersion = analysis.AnalyzerVersion,
            RawAiResponse = analysis.RawAiResponse,
            AnalyzedAt = analysis.AnalyzedAt,
            CreatedAt = analysis.CreatedAt
        };
    }
}
