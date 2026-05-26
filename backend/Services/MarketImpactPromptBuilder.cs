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
              "direction": 0,
              "reasoning": "Short explanation.",
              "affectedAssets": ["US equities"]
            }

            Rules:
            - marketImpactScore must be an integer from 1 to 100.
            - confidenceScore must be an integer from 1 to 100.
            - direction must be an integer between -50 and 50. Negative values mean negative expected market direction. Positive values mean positive expected market direction. 0 means neutral/no expected directional market impact.
            - reasoning must be a short explanation, preferably one or two sentences.
            - affectedAssets must be an array of strings.
            - If the post is vague or market relevance is unclear, use lower confidenceScore and direction 0.
            - If market impact is low, use a low marketImpactScore and direction 0 unless there is a clear directional market signal.

            Examples:
            - Tariffs, China, trade war, sanctions, inflation, recession, or rate hikes usually imply a direction below 0.
            - Federal Reserve, inflation, or interest rates usually means medium or high impact.
            - Jobs growth, tax cuts, lower rates, or trade deals usually imply a direction above 0.
            - If competing directional indicators appear, use a value near 0.
            - Thank you, rally, birthday, congratulations, endorsement, or crowd-size content usually means low impact and direction 0.
            - Vague or unclear content should use lower confidenceScore and direction 0.

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
            Direction = -30,
            Reasoning = "The post references tariffs and China, which can affect trade-sensitive equities and currency markets.",
            AffectedAssets = ["US equities", "China-related equities", "USD"]
        };
    }
}
