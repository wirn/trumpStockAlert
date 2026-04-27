using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public sealed class MarketImpactPromptBuilder
{
    public string BuildPrompt(TruthPost post)
    {
        return $$"""
            You are analyzing a public Truth Social post for possible financial market impact.

            Return only valid JSON.
            Do not include markdown.
            Do not include code fences.
            Do not include explanations outside the JSON object.

            Analyze whether the post could affect public financial markets, including equities, bonds, currencies, commodities, crypto, sectors, or companies.

            Use this exact JSON schema:
            {
              "marketImpactScore": 1,
              "direction": "Positive",
              "reasoning": "Short explanation.",
              "affectedAssets": ["US equities"],
              "confidence": 1
            }

            Rules:
            - marketImpactScore must be an integer from 1 to 100.
            - direction must be one of: Positive, Negative, Neutral, Uncertain.
            - reasoning must be a short explanation, preferably one or two sentences.
            - affectedAssets must be an array of strings.
            - confidence must be an integer from 1 to 100.
            - If the post is vague or market relevance is unclear, use lower confidence and direction Uncertain.
            - If market impact is low, use a low marketImpactScore and direction Neutral unless there is a clear positive or negative market signal.

            Examples:
            - Tariffs, China, or trade war language usually means high impact and often Negative.
            - Federal Reserve, inflation, or interest rates usually means medium or high impact.
            - Thank you, rally, birthday, congratulations, endorsement, or crowd-size content usually means low impact and Neutral.
            - Vague or unclear content should use lower confidence and Uncertain.

            Post metadata:
            - Source: {{post.Source}}
            - Author: {{post.Author}}
            - ExternalId: {{post.ExternalId}}
            - CreatedAtUtc: {{post.CreatedAt.UtcDateTime:O}}

            Post content:
            {{post.Content}}
            """;
    }

    public MarketImpactAiResponse BuildExampleResponse()
    {
        return new MarketImpactAiResponse
        {
            MarketImpactScore = 85,
            Direction = "Negative",
            Reasoning = "The post references tariffs and China, which can affect trade-sensitive equities and currency markets.",
            AffectedAssets = ["US equities", "China-related equities", "USD"],
            Confidence = 75
        };
    }
}
