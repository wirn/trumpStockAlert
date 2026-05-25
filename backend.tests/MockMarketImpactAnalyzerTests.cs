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
    [InlineData("The Federal Reserve must lower interest rates now")]
    [InlineData("Sanctions on Russia are in place")]
    public async Task AnalyzeAsync_HighImpactKeywords_ReturnsHighScore(string content)
    {
        var post = MakePost(content);

        var result = await _analyzer.AnalyzeAsync(post);

        Assert.True(result.MarketImpactScore >= 80,
            $"Expected score >= 80 for high-impact content, got {result.MarketImpactScore}");
    }

    [Theory]
    [InlineData("The stock market is looking great today")]
    [InlineData("Jobs numbers are up, economy is strong")]
    [InlineData("Bitcoin and crypto are the future")]
    public async Task AnalyzeAsync_MediumImpactKeywords_ReturnsMediumScore(string content)
    {
        var post = MakePost(content);

        var result = await _analyzer.AnalyzeAsync(post);

        Assert.InRange(result.MarketImpactScore, 50, 79);
    }

    [Theory]
    [InlineData("Thank you to everyone at the rally tonight!")]
    [InlineData("Congratulations to our great endorsement winners")]
    public async Task AnalyzeAsync_LowImpactKeywords_ReturnsLowScore(string content)
    {
        var post = MakePost(content);

        var result = await _analyzer.AnalyzeAsync(post);

        Assert.True(result.MarketImpactScore < 50,
            $"Expected score < 50 for low-impact content, got {result.MarketImpactScore}");
    }

    [Theory]
    [InlineData("tariffs on China will crush trade")]
    [InlineData("sanctions imposed on Iran")]
    [InlineData("trade war escalating rapidly")]
    public async Task AnalyzeAsync_NegativeKeywords_ReturnsNegativeDirection(string content)
    {
        var post = MakePost(content);

        var result = await _analyzer.AnalyzeAsync(post);

        Assert.True(result.Direction < 0,
            $"Expected negative direction for '{content}', got {result.Direction}");
    }

    [Theory]
    [InlineData("tax cuts for all Americans will grow the economy")]
    [InlineData("deregulation is creating jobs across the country")]
    public async Task AnalyzeAsync_PositiveKeywords_ReturnsPositiveDirection(string content)
    {
        var post = MakePost(content);

        var result = await _analyzer.AnalyzeAsync(post);

        Assert.True(result.Direction > 0,
            $"Expected positive direction for '{content}', got {result.Direction}");
    }

    [Fact]
    public async Task AnalyzeAsync_LowImpactContent_ReturnsNeutralDirection()
    {
        var post = MakePost("Thank you to everyone at the rally tonight!");

        var result = await _analyzer.AnalyzeAsync(post);

        Assert.Equal(0, result.Direction);
    }

    [Fact]
    public async Task AnalyzeAsync_ScoreIsAlwaysInRange()
    {
        var posts = new[]
        {
            "tariffs on China now",
            "great rally tonight!",
            "stock market at all-time high",
            "just had a great birthday party",
            string.Empty
        };

        foreach (var content in posts)
        {
            var result = await _analyzer.AnalyzeAsync(MakePost(content));
            Assert.InRange(result.MarketImpactScore, 1, 100);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_DirectionIsAlwaysInRange()
    {
        var posts = new[]
        {
            "tariffs on China now",
            "tax cuts for all",
            "thank you rally crowd",
            string.Empty
        };

        foreach (var content in posts)
        {
            var result = await _analyzer.AnalyzeAsync(MakePost(content));
            Assert.InRange(result.Direction, -50, 50);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_ConfidenceIsAlwaysInRange()
    {
        var post = MakePost("tariffs on China now");

        var result = await _analyzer.AnalyzeAsync(post);

        Assert.InRange(result.Confidence, 1, 100);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsNonEmptyReasoning()
    {
        var post = MakePost("tariffs on China now");

        var result = await _analyzer.AnalyzeAsync(post);

        Assert.False(string.IsNullOrWhiteSpace(result.Reasoning));
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsNonEmptyAffectedAssets()
    {
        var post = MakePost("tariffs on China now");

        var result = await _analyzer.AnalyzeAsync(post);

        Assert.NotEmpty(result.AffectedAssets);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsAnalyzerVersion()
    {
        var post = MakePost("tariffs on China now");

        var result = await _analyzer.AnalyzeAsync(post);

        Assert.False(string.IsNullOrWhiteSpace(result.AnalyzerVersion));
    }

    [Fact]
    public async Task AnalyzeAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var post = MakePost("some content");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _analyzer.AnalyzeAsync(post, cts.Token));
    }
}
