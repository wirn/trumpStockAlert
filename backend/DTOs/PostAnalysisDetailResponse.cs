using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.DTOs;

public sealed class PostAnalysisDetailResponse
{
    public required int Id { get; init; }

    public required int PostId { get; init; }

    public required string PostContent { get; init; }

    public required string PostUrl { get; init; }

    public required DateTimeOffset PostCreatedAt { get; init; }

    public required int MarketImpactScore { get; init; }

    public required int Confidence { get; init; }

    public required int Direction { get; init; }

    public required string Reasoning { get; init; }

    public string? AffectedAssetsJson { get; init; }

    public required DateTimeOffset AnalyzedAt { get; init; }

    public required string AnalyzerVersion { get; init; }

    public static PostAnalysisDetailResponse FromEntity(PostAnalysis analysis) =>
        new()
        {
            Id = analysis.Id,
            PostId = analysis.PostId,
            PostContent = analysis.Post.Content,
            PostUrl = analysis.Post.Url,
            PostCreatedAt = analysis.Post.CreatedAt,
            MarketImpactScore = analysis.MarketImpactScore,
            Confidence = analysis.Confidence,
            Direction = analysis.Direction,
            Reasoning = analysis.Reasoning,
            AffectedAssetsJson = analysis.AffectedAssetsJson,
            AnalyzedAt = analysis.AnalyzedAt,
            AnalyzerVersion = analysis.AnalyzerVersion,
        };
}
