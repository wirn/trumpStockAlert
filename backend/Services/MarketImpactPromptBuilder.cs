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

            Evaluate market impact, not political importance.

            Score expected market impact over the next 1-5 trading days.
            Do not increase marketImpactScore simply because a topic is politically important.
            Focus on whether the information is new, unexpected, credible, and likely to change market expectations.
            Score the post from the perspective of a professional macro investor deciding whether the information could influence:

            - asset prices
            - market expectations
            - corporate earnings
            - interest rates
            - inflation
            - taxation
            - trade policy
            - regulation
            - government spending
            - geopolitical risk
            - capital flows

            Analyze whether the post could affect public financial markets, including equities, bonds, currencies, commodities, crypto, sectors, or companies.

            Use this exact JSON schema:
            {
            "marketImpactScore": 1,
            "confidenceScore": 1,
            "direction": 0,
            "category": "Other",
            "reasoning": "Kort förklaring på svenska.",
            "affectedAssets": ["US equities"]
            }

            Rules:
            - reasoning must ALWAYS be written in Swedish.
            - reasoning should be one or two concise sentences in Swedish.
            - affectedAssets should remain in English financial terminology when appropriate.
            - marketImpactScore must be an integer from 1 to 100.
            - confidenceScore must be an integer from 1 to 100.
            - direction must be an integer between -50 and 50.
            - reasoning must be a short explanation, preferably one or two sentences.
            - affectedAssets must be an array of strings.
            - Campaign events, political rallies, polling results, endorsements, crowd sizes, and personal attacks usually have little or no market impact unless they contain new policy information.

            marketImpactScore guidance:

            - 1-10 = negligible impact
            - 11-30 = minor impact
            - 31-50 = moderate impact
            - 51-70 = significant impact
            - 71-90 = major market-moving event
            - 91-100 = extraordinary market-moving event

            direction guidance:

            - Negative values imply expected negative market impact.
            - Positive values imply expected positive market impact.
            - 0 means no clear directional signal.

            confidenceScore guidance:

            - Use higher values when the market implications are clear and direct.
            - Use lower values when the message is vague, ambiguous, speculative, or difficult to interpret.

            - category must be exactly one of the allowed category values listed below.
            Trade
            Tariffs
            MonetaryPolicy
            FiscalPolicy
            Inflation
            Employment
            Energy
            Defense
            Geopolitics
            Elections
            Regulation
            Crypto
            Corporate
            Other

            Examples:

            - Tariffs, China, trade war, sanctions, inflation, recession, or rate hikes usually imply a direction below 0.
            - Federal Reserve, inflation, or interest rates usually imply medium or high market impact.
            - Jobs growth, tax cuts, lower rates, or trade deals usually imply a direction above 0.
            - If competing directional indicators appear, use a value near 0.
            - Thank you, rally, birthday, congratulations, endorsement, crowd-size, polling, or campaign content usually implies low impact and direction 0 unless a clear market implication exists.

            Additional rules:

            - If the post is vague or market relevance is unclear, use lower confidenceScore and direction 0.
            - If market impact is low, use a low marketImpactScore and direction 0 unless there is a clear directional signal.
            - A politically important post can still have a low marketImpactScore.
            - Avoid overestimating market impact.

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
            Category = "Tariffs",
            Reasoning = "Inlägget hänvisar till tullar och Kina, vilket kan påverka handelsberoende aktier, leverantörskedjor och valutamarknader.",
            AffectedAssets =
            [
                "US equities",
                "China-related equities",
                "USD"
            ]
        };
    }
}