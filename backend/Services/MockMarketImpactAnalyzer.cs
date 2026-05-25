using System.Text.Json;
using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public sealed class MockMarketImpactAnalyzer : IMarketImpactAnalyzer
{
    private const string Version = "mock-keyword-v2";

    private static readonly string[] HighImpactKeywords =
    [
        "tariff",
        "tariffs",
        "china",
        "fed",
        "federal reserve",
        "interest rate",
        "interest rates",
        "inflation",
        "oil",
        "sanctions",
        "war",
        "crypto",
        "bitcoin",
        "tesla",
        "nvidia"
    ];

    private static readonly string[] MediumImpactKeywords =
    [
        "election",
        "economy",
        "jobs",
        "taxes",
        "regulation",
        "trade",
        "immigration"
    ];

    private static readonly string[] LowImpactKeywords =
    [
        "thank you",
        "great crowd",
        "rally",
        "endorsement",
        "congratulations",
        "campaign",
        "social"
    ];

    private static readonly string[] NegativeDirectionKeywords =
    [
        "tariff",
        "tariffs",
        "sanctions",
        "war",
        "recession",
        "inflation",
        "rate hikes"
    ];

    private static readonly string[] PositiveDirectionKeywords =
    [
        "tax cuts",
        "growth",
        "jobs growth",
        "lower rates",
        "trade deal"
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
        var negativeMatches = FindMatches(content, NegativeDirectionKeywords);
        var positiveMatches = FindMatches(content, PositiveDirectionKeywords);

        var score = GetMarketImpactScore(highMatches, mediumMatches, lowMatches);
        var confidenceScore = GetConfidenceScore(highMatches, mediumMatches, lowMatches, positiveMatches, negativeMatches);
        var direction = GetDirection(positiveMatches, negativeMatches);
        var affectedAssets = GetAffectedAssets(content);
        var reasoning = GetReasoning(highMatches, mediumMatches, lowMatches, positiveMatches, negativeMatches, affectedAssets);
        var rawAiResponse = JsonResponseText.NormalizeObjectJson(JsonSerializer.Serialize(new
        {
            marketImpactScore = score,
            confidenceScore,
            direction,
            reasoning,
            affectedAssets
        }));

        return Task.FromResult(new MarketImpactAnalysisResult
        {
            MarketImpactScore = score,
            ConfidenceScore = confidenceScore,
            Direction = direction,
            Reasoning = reasoning,
            AffectedAssets = affectedAssets,
            AnalyzerVersion = Version,
            RawAiResponse = rawAiResponse
        });
    }

    private static int GetMarketImpactScore(
        IReadOnlyCollection<string> highMatches,
        IReadOnlyCollection<string> mediumMatches,
        IReadOnlyCollection<string> lowMatches)
    {
        if (highMatches.Count > 0)
        {
            return Math.Min(100, 78 + highMatches.Count * 4);
        }

        if (mediumMatches.Count > 0)
        {
            return Math.Min(69, 46 + mediumMatches.Count * 5);
        }

        if (lowMatches.Count > 0)
        {
            return Math.Min(39, 18 + lowMatches.Count * 4);
        }

        return 25;
    }

    private static int GetConfidenceScore(
        IReadOnlyCollection<string> highMatches,
        IReadOnlyCollection<string> mediumMatches,
        IReadOnlyCollection<string> lowMatches,
        IReadOnlyCollection<string> positiveMatches,
        IReadOnlyCollection<string> negativeMatches)
    {
        var matchCount = highMatches.Count
            + mediumMatches.Count
            + lowMatches.Count
            + positiveMatches.Count
            + negativeMatches.Count;

        if (matchCount == 0)
        {
            return 45;
        }

        return Math.Clamp(58 + matchCount * 6, 1, 92);
    }

    private static string GetDirection(
        IReadOnlyCollection<string> positiveMatches,
        IReadOnlyCollection<string> negativeMatches)
    {
        if (positiveMatches.Count > 0 && negativeMatches.Count > 0)
        {
            return "mixed";
        }

        if (negativeMatches.Count > 0)
        {
            return "negative";
        }

        if (positiveMatches.Count > 0)
        {
            return "positive";
        }

        return "neutral";
    }

    private static IReadOnlyList<string> GetAffectedAssets(string content)
    {
        var assets = new List<string>();

        AddAssetsWhenMatched(content, ["tariff", "tariffs", "china", "trade"], assets, ["SP500", "USD", "China equities"]);
        AddAssetsWhenMatched(content, ["fed", "federal reserve", "interest rate", "interest rates", "inflation", "lower rates", "rate hikes"], assets, ["US equities", "Treasuries", "USD"]);
        AddAssetsWhenMatched(content, ["oil"], assets, ["Oil", "Energy stocks"]);
        AddAssetsWhenMatched(content, ["crypto", "bitcoin"], assets, ["Bitcoin", "Crypto equities"]);
        AddAssetsWhenMatched(content, ["tesla"], assets, ["Tesla", "EV sector"]);
        AddAssetsWhenMatched(content, ["nvidia"], assets, ["Nvidia", "Semiconductors"]);

        return assets.Count > 0 ? assets : ["US equities"];
    }

    private static void AddAssetsWhenMatched(
        string content,
        IReadOnlyList<string> keywords,
        ICollection<string> assets,
        IReadOnlyList<string> assetsToAdd)
    {
        if (!ContainsAny(content, keywords))
        {
            return;
        }

        foreach (var asset in assetsToAdd)
        {
            if (!assets.Contains(asset))
            {
                assets.Add(asset);
            }
        }
    }

    private static string GetReasoning(
        IReadOnlyCollection<string> highMatches,
        IReadOnlyCollection<string> mediumMatches,
        IReadOnlyCollection<string> lowMatches,
        IReadOnlyCollection<string> positiveMatches,
        IReadOnlyCollection<string> negativeMatches,
        IReadOnlyList<string> affectedAssets)
    {
        var impactMatches = highMatches.Count > 0
            ? highMatches
            : mediumMatches.Count > 0
                ? mediumMatches
                : lowMatches;

        if (impactMatches.Count == 0)
        {
            return "No strong market keywords were detected, so the mock analyzer returned a low-confidence neutral assessment.";
        }

        var impactPhrase = highMatches.Count > 0
            ? "high-impact"
            : mediumMatches.Count > 0
                ? "market-related"
                : "low-impact social or campaign";

        var directionPhrase = GetDirectionPhrase(positiveMatches, negativeMatches);
        return $"Mentions {string.Join(" and ", impactMatches.Take(3))}, a {impactPhrase} signal that could affect {string.Join(", ", affectedAssets.Take(3))}. {directionPhrase}";
    }

    private static string GetDirectionPhrase(
        IReadOnlyCollection<string> positiveMatches,
        IReadOnlyCollection<string> negativeMatches)
    {
        if (positiveMatches.Count > 0 && negativeMatches.Count > 0)
        {
            return "Positive and negative indicators are both present, so direction is mixed.";
        }

        if (negativeMatches.Count > 0)
        {
            return "Negative indicators suggest pressure on market sentiment.";
        }

        if (positiveMatches.Count > 0)
        {
            return "Positive indicators suggest supportive market sentiment.";
        }

        return "No clear directional indicators were detected, so direction is neutral.";
    }

    private static IReadOnlyList<string> FindMatches(string content, IReadOnlyList<string> keywords)
    {
        return keywords
            .Where(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ContainsAny(string content, IReadOnlyList<string> keywords)
    {
        return keywords.Any(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
