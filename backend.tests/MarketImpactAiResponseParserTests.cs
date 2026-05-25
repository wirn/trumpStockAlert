using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Tests;

public sealed class MarketImpactAiResponseParserTests
{
    private readonly MarketImpactAiResponseParser _parser = new();

    private static string BuildJson(
        int marketImpactScore = 50,
        int direction = 0,
        string reasoning = "Some reasoning.",
        string affectedAssets = @"[""US equities""]",
        int confidence = 50)
    {
        return $$"""
            {
              "marketImpactScore": {{marketImpactScore}},
              "direction": {{direction}},
              "reasoning": "{{reasoning}}",
              "affectedAssets": {{affectedAssets}},
              "confidence": {{confidence}}
            }
            """;
    }

    [Fact]
    public void ParseAndValidate_ValidResponse_ReturnsCorrectValues()
    {
        var json = BuildJson(marketImpactScore: 75, direction: -35, confidence: 70);

        var result = _parser.ParseAndValidate(json);

        Assert.Equal(75, result.MarketImpactScore);
        Assert.Equal(-35, result.Direction);
        Assert.Equal(70, result.Confidence);
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
    [InlineData(-50)]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(-1)]
    [InlineData(25)]
    [InlineData(-25)]
    public void ParseAndValidate_DirectionInRange_DoesNotThrow(int direction)
    {
        var json = BuildJson(direction: direction);

        var result = _parser.ParseAndValidate(json);

        Assert.Equal(direction, result.Direction);
    }

    [Theory]
    [InlineData(-51)]
    [InlineData(51)]
    [InlineData(-100)]
    [InlineData(100)]
    public void ParseAndValidate_DirectionOutOfRange_Throws(int direction)
    {
        var json = BuildJson(direction: direction);

        var ex = Assert.Throws<MarketImpactAiResponseParseException>(
            () => _parser.ParseAndValidate(json));

        Assert.Contains("direction", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void ParseAndValidate_ConfidenceOutOfRange_Throws(int confidence)
    {
        var json = BuildJson(confidence: confidence);

        var ex = Assert.Throws<MarketImpactAiResponseParseException>(
            () => _parser.ParseAndValidate(json));

        Assert.Contains("confidence", ex.Message);
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
