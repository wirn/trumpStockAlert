using System.Text.Json;
using TrumpStockAlert.Api.Models;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Tests;

public sealed class MockMarketImpactAnalyzerTests
{
    private readonly MockMarketImpactAnalyzer _analyzer = new();

    private static TruthPost MakePost(string content) => new()
    {
        Id = 1,
        Source = "truth_social",
        Author = "realDonaldTrump",
        ExternalId = "123",
        Url = "https://example.com/post/123",
        Content = content,
        CreatedAt = DateTimeOffset.UtcNow,
        CollectedAt = DateTimeOffset.UtcNow,
        SavedAtUtc = DateTimeOffset.UtcNow
    };

    [Theory]
    [InlineData("We are placing huge tariffs on China products")]
    [InlineData("The Fed must lower interest rates now")]
    [InlineData("Tesla and Nvidia are moving fast in this market")]
    public async Task AnalyzeAsync_HighImpactKeywords_ReturnsHighScore(string content)
    {
        var result = await _analyzer.AnalyzeAsync(MakePost(content));

        Assert.InRange(result.MarketImpactScore, 70, 100);
    }

    [Theory]
    [InlineData("Thank you to everyone at the rally tonight!")]
    [InlineData("Congratulations to our great endorsement winners")]
    public async Task AnalyzeAsync_LowImpactKeywords_ReturnsLowScore(string content)
    {
        var result = await _analyzer.AnalyzeAsync(MakePost(content));

        Assert.InRange(result.MarketImpactScore, 1, 39);
        Assert.Equal(0, result.Direction);
    }

    [Fact]
    public async Task AnalyzeAsync_MixedDirectionKeywords_ReturnsNeutralDirection()
    {
        var result = await _analyzer.AnalyzeAsync(
            MakePost("Tariffs are coming, but a trade deal and tax cuts will support growth."));

        Assert.Equal(0, result.Direction);
    }

    [Fact]
    public async Task AnalyzeAsync_AffectedAssets_ArePopulatedFromKeywords()
    {
        var result = await _analyzer.AnalyzeAsync(
            MakePost("Tariffs on China, Fed rates, oil, Bitcoin, Tesla, and Nvidia are all in focus."));

        Assert.Contains("SP500", result.AffectedAssets);
        Assert.Contains("China equities", result.AffectedAssets);
        Assert.Contains("Treasuries", result.AffectedAssets);
        Assert.Contains("Oil", result.AffectedAssets);
        Assert.Contains("Bitcoin", result.AffectedAssets);
        Assert.Contains("Tesla", result.AffectedAssets);
        Assert.Contains("Nvidia", result.AffectedAssets);
    }

    [Theory]
    [InlineData("tariffs on China now")]
    [InlineData("great rally tonight!")]
    [InlineData("economy and jobs")]
    [InlineData("")]
    public async Task AnalyzeAsync_ConfidenceScoreIsAlwaysInRange(string content)
    {
        var result = await _analyzer.AnalyzeAsync(MakePost(content));

        Assert.InRange(result.ConfidenceScore, 1, 100);
    }

    [Fact]
    public async Task AnalyzeAsync_IsCaseInsensitive()
    {
        var result = await _analyzer.AnalyzeAsync(MakePost("BITCOIN and CRYPTO are moving."));

        Assert.InRange(result.MarketImpactScore, 70, 100);
        Assert.Contains("Bitcoin", result.AffectedAssets);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsFinalContractShapeInRawResponse()
    {
        var result = await _analyzer.AnalyzeAsync(MakePost("Tariffs on China affect trade."));
        using var document = JsonDocument.Parse(result.RawAiResponse);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("marketImpactScore", out _));
        Assert.True(root.TryGetProperty("confidenceScore", out _));
        Assert.True(root.TryGetProperty("direction", out var direction));
        Assert.True(root.TryGetProperty("reasoning", out _));
        Assert.True(root.TryGetProperty("affectedAssets", out _));
        Assert.Equal(-25, direction.GetInt32());
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsNonEmptyReasoningAndAnalyzerVersion()
    {
        var result = await _analyzer.AnalyzeAsync(MakePost("tariffs on China now"));

        Assert.False(string.IsNullOrWhiteSpace(result.Reasoning));
        Assert.False(string.IsNullOrWhiteSpace(result.AnalyzerVersion));
    }

    [Fact]
    public async Task AnalyzeAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _analyzer.AnalyzeAsync(MakePost("some content"), cts.Token));
    }
}
