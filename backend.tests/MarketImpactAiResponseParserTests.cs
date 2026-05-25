using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Tests;

public sealed class MarketImpactAiResponseParserTests
{
    private readonly MarketImpactAiResponseParser _parser = new();

    private static string BuildJson(
        int marketImpactScore = 50,
        int confidenceScore = 50,
        string direction = "neutral",
        string reasoning = "Some reasoning.",
        string affectedAssets = @"[""US equities""]")
    {
        return $$"""
            {
              "marketImpactScore": {{marketImpactScore}},
              "confidenceScore": {{confidenceScore}},
              "direction": "{{direction}}",
              "reasoning": "{{reasoning}}",
              "affectedAssets": {{affectedAssets}}
            }
            """;
    }

    [Fact]
    public void ParseAndValidate_ValidResponse_ReturnsCorrectValues()
    {
        var json = BuildJson(marketImpactScore: 75, confidenceScore: 70, direction: "negative");

        var result = _parser.ParseAndValidate(json);

        Assert.Equal(75, result.MarketImpactScore);
        Assert.Equal(70, result.ConfidenceScore);
        Assert.Equal("negative", result.Direction);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-1)]
    public void ParseAndValidate_MarketImpactScoreOutOfRange_Throws(int score)
    {
        var json = BuildJson(marketImpactScore: score);

        var ex = Assert.Throws<MarketImpactAiResponseParseException>(
            () => _parser.ParseAndValidate(json));

        Assert.Contains("marketImpactScore", ex.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(50)]
    public void ParseAndValidate_MarketImpactScoreInRange_DoesNotThrow(int score)
    {
        var json = BuildJson(marketImpactScore: score);

        var result = _parser.ParseAndValidate(json);

        Assert.Equal(score, result.MarketImpactScore);
    }

    [Theory]
    [InlineData("positive")]
    [InlineData("negative")]
    [InlineData("neutral")]
    [InlineData("mixed")]
    [InlineData("MIXED")]
    public void ParseAndValidate_AllowedDirection_DoesNotThrow(string direction)
    {
        var json = BuildJson(direction: direction);

        var result = _parser.ParseAndValidate(json);

        Assert.Equal(direction.ToLowerInvariant(), result.Direction);
    }

    [Theory]
    [InlineData("bullish")]
    [InlineData("")]
    [InlineData("uncertain")]
    public void ParseAndValidate_InvalidDirection_Throws(string direction)
    {
        var json = BuildJson(direction: direction);

        var ex = Assert.Throws<MarketImpactAiResponseParseException>(
            () => _parser.ParseAndValidate(json));

        Assert.Contains("direction", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void ParseAndValidate_ConfidenceScoreOutOfRange_Throws(int confidenceScore)
    {
        var json = BuildJson(confidenceScore: confidenceScore);

        var ex = Assert.Throws<MarketImpactAiResponseParseException>(
            () => _parser.ParseAndValidate(json));

        Assert.Contains("confidenceScore", ex.Message);
    }

    [Fact]
    public void ParseAndValidate_EmptyJson_Throws()
    {
        Assert.Throws<MarketImpactAiResponseParseException>(
            () => _parser.ParseAndValidate(""));
    }

    [Fact]
    public void ParseAndValidate_InvalidJson_Throws()
    {
        Assert.Throws<MarketImpactAiResponseParseException>(
            () => _parser.ParseAndValidate("not json"));
    }
}
