using System.Text.Json;

namespace TrumpStockAlert.Api.Services;

public sealed class MarketImpactAiResponseParser
{
    private static readonly HashSet<string> ValidDirections = new(StringComparer.Ordinal)
    {
        "Positive",
        "Negative",
        "Neutral",
        "Uncertain"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MarketImpactAiResponse ParseAndValidate(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new MarketImpactAiResponseParseException("AI response JSON is required.");
        }

        var normalizedJson = NormalizeJsonForParsing(rawJson);
        MarketImpactAiResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<MarketImpactAiResponse>(
                normalizedJson,
                SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new MarketImpactAiResponseParseException(
                $"AI response is not valid JSON: {exception.Message}");
        }

        if (response is null)
        {
            throw new MarketImpactAiResponseParseException("AI response JSON could not be parsed.");
        }

        Validate(response);
        return response;
    }

    public string NormalizeAndValidate(string rawJson)
    {
        var normalizedJson = NormalizeJsonForParsing(rawJson);
        ParseAndValidate(normalizedJson);
        return normalizedJson;
    }

    private static string NormalizeJsonForParsing(string rawJson)
    {
        try
        {
            return JsonResponseText.NormalizeObjectJson(rawJson);
        }
        catch (JsonException exception)
        {
            throw new MarketImpactAiResponseParseException(
                $"AI response is not valid JSON: {exception.Message}");
        }
    }

    private static void Validate(MarketImpactAiResponse response)
    {
        if (response.MarketImpactScore is < 1 or > 100)
        {
            throw new MarketImpactAiResponseParseException("marketImpactScore must be an integer from 1 to 100.");
        }

        if (string.IsNullOrWhiteSpace(response.Direction))
        {
            throw new MarketImpactAiResponseParseException("direction is required.");
        }

        if (!ValidDirections.Contains(response.Direction))
        {
            throw new MarketImpactAiResponseParseException(
                "direction must be one of: Positive, Negative, Neutral, Uncertain.");
        }

        if (string.IsNullOrWhiteSpace(response.Reasoning))
        {
            throw new MarketImpactAiResponseParseException("reasoning is required.");
        }

        if (response.AffectedAssets is null)
        {
            throw new MarketImpactAiResponseParseException("affectedAssets is required and must be an array of strings.");
        }

        if (response.AffectedAssets.Any(string.IsNullOrWhiteSpace))
        {
            throw new MarketImpactAiResponseParseException("affectedAssets must contain only non-empty strings.");
        }

        if (response.Confidence is < 1 or > 100)
        {
            throw new MarketImpactAiResponseParseException("confidence must be an integer from 1 to 100.");
        }
    }
}
