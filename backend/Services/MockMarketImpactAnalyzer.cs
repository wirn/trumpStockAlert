using System.Text.Json;
using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public sealed class MockMarketImpactAnalyzer : IMarketImpactAnalyzer
{
    private const string Version = "mock-keyword-v1";

    private static readonly string[] HighImpactKeywords =
    [
        "tariff",
        "tariffs",
        "china",
        "federal reserve",
        "fed",
        "interest rate",
        "inflation",
        "oil",
        "sanctions",
        "trade war"
    ];

    private static readonly string[] MediumImpactKeywords =
    [
        "economy",
        "stock market",
        "market",
        "dollar",
        "usd",
        "jobs",
        "unemployment",
        "taxes",
        "regulation",
        "crypto",
        "bitcoin"
    ];

    private static readonly string[] LowImpactKeywords =
    [
        "thank you",
        "great crowd",
        "rally",
        "endorsement",
        "birthday",
        "congratulations",
        "interview",
        "poll"
    ];

    private static readonly string[] NegativeDirectionKeywords =
    [
        "tariff",
        "tariffs",
        "sanctions",
        "trade war",
        "inflation",
        "interest rate",
        "oil"
    ];

    private static readonly string[] PositiveDirectionKeywords =
    [
        "jobs",
        "tax cuts",
        "deregulation",
        "strong economy",
        "stock market up"
    ];

    public Task<MarketImpactAnalysisResult> AnalyzeAsync(
        TruthPost post,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var content = post.Content ?? string.Empty;
        var highMatches = FindMatches(content, HighImpactKeywords);
        var mediumMatches = FindMatches(content, MediumImpactKeywords);
        var lowMatches = FindMatches(content, LowImpactKeywords);
        var isHighImpact = highMatches.Count > 0;
        var isMediumImpact = mediumMatches.Count > 0;
        var isLowImpact = lowMatches.Count > 0;

        var score = GetScore(isHighImpact, isMediumImpact, isLowImpact);
        var direction = GetDirection(content, isLowImpact);
        var affectedAssets = GetAffectedAssets(content);
        var reasoning = GetReasoning(score, direction, highMatches, mediumMatches, lowMatches);
        var confidence = GetConfidence(isHighImpact, isMediumImpact, isLowImpact);
        var rawAiResponse = JsonSerializer.Serialize(new
        {
            analyzer = Version,
            mode = "mock",
            matchedKeywords = new
            {
                highImpact = highMatches,
                mediumImpact = mediumMatches,
                lowImpact = lowMatches
            }
        });

        var result = new MarketImpactAnalysisResult
        {
            MarketImpactScore = score,
            Direction = direction,
            Reasoning = reasoning,
            AffectedAssets = affectedAssets,
            Confidence = confidence,
            AnalyzerVersion = Version,
            RawAiResponse = rawAiResponse
        };

        return Task.FromResult(result);
    }

    private static int GetScore(bool isHighImpact, bool isMediumImpact, bool isLowImpact)
    {
        if (isHighImpact)
        {
            return 85;
        }

        if (isMediumImpact)
        {
            return 68;
        }

        if (isLowImpact)
        {
            return 20;
        }

        return 40;
    }

    private static string GetDirection(string content, bool isLowImpact)
    {
        if (ContainsAny(content, NegativeDirectionKeywords))
        {
            return "Negative";
        }

        if (ContainsAny(content, PositiveDirectionKeywords))
        {
            return "Positive";
        }

        if (isLowImpact)
        {
            return "Neutral";
        }

        return "Uncertain";
    }

    private static IReadOnlyList<string> GetAffectedAssets(string content)
    {
        if (ContainsAny(content, ["china", "tariff", "tariffs", "trade war"]))
        {
            return ["US equities", "China-related equities", "USD"];
        }

        if (ContainsAny(content, ["fed", "federal reserve", "interest rate", "inflation"]))
        {
            return ["US equities", "bonds", "USD"];
        }

        if (ContainsAny(content, ["oil", "sanctions"]))
        {
            return ["energy stocks", "oil", "US equities"];
        }

        if (ContainsAny(content, ["crypto", "bitcoin"]))
        {
            return ["crypto", "bitcoin-related equities"];
        }

        return ["US equities"];
    }

    private static string GetReasoning(
        int score,
        string direction,
        IReadOnlyList<string> highMatches,
        IReadOnlyList<string> mediumMatches,
        IReadOnlyList<string> lowMatches)
    {
        if (highMatches.Count > 0)
        {
            return $"Mock analysis found high-impact market keywords ({string.Join(", ", highMatches)}), producing a {direction.ToLowerInvariant()} score of {score}.";
        }

        if (mediumMatches.Count > 0)
        {
            return $"Mock analysis found market-related keywords ({string.Join(", ", mediumMatches)}), producing a {direction.ToLowerInvariant()} score of {score}.";
        }

        if (lowMatches.Count > 0)
        {
            return $"Mock analysis found mostly low-market-impact political or event keywords ({string.Join(", ", lowMatches)}), producing a neutral score of {score}.";
        }

        return "Mock analysis found no strong market-impact keywords, so it returned a default uncertain score.";
    }

    private static int GetConfidence(bool isHighImpact, bool isMediumImpact, bool isLowImpact)
    {
        if (isHighImpact)
        {
            return 75;
        }

        if (isMediumImpact)
        {
            return 65;
        }

        if (isLowImpact)
        {
            return 70;
        }

        return 50;
    }

    private static IReadOnlyList<string> FindMatches(string content, IReadOnlyList<string> keywords)
    {
        return keywords
            .Where(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool ContainsAny(string content, IReadOnlyList<string> keywords)
    {
        return keywords.Any(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
