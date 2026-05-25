using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public sealed class MarketImpactPromptBuilder
{
    public string BuildPrompt(TruthPost post)
    {
        return $$"""
            You are analyzing a public Truth Social post for possible financial market impact.

            Return only valid JSON matching this exact contract.
            Do not include markdown.
            Do not include code fences.
            Do not include explanations outside the JSON object.

            Analyze whether the post could affect public financial markets, including equities, bonds, currencies, commodities, crypto, sectors, or companies.

            Use this exact JSON schema:
            {
              "marketImpactScore": 1,
              "confidenceScore": 1,
              "direction": "neutral",
              "reasoning": "Short explanation.",
              "affectedAssets": ["US equities"]
            }

            Rules:
            - marketImpactScore must be an integer from 1 to 100.
            - confidenceScore must be an integer from 1 to 100.
            - direction must be exactly one of: positive, negative, neutral, mixed.
            - reasoning must be a short explanation, preferably one or two sentences.
            - affectedAssets must be an array of strings.
            - If the post is vague or market relevance is unclear, use lower confidenceScore and neutral direction.
            - If market impact is low, use a low marketImpactScore and neutral direction unless there is a clear positive or negative market signal.

            Examples:
            - Tariffs, China, trade war, sanctions, inflation, recession, or rate hikes usually imply negative direction.
            - Federal Reserve, inflation, or interest rates usually means medium or high impact.
            - Jobs growth, tax cuts, lower rates, or trade deals usually imply positive direction.
            - If both positive and negative indicators appear, use mixed direction.
            - Thank you, rally, birthday, congratulations, endorsement, or crowd-size content usually means low impact and neutral direction.
            - Vague or unclear content should use lower confidenceScore and neutral direction.

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
            ConfidenceScore = 75,
            Direction = "negative",
            Reasoning = "The post references tariffs and China, which can affect trade-sensitive equities and currency markets.",
            AffectedAssets = ["US equities", "China-related equities", "USD"]
        };
    }
}
