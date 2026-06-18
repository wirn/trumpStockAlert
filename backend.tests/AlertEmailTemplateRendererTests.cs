using TrumpStockAlert.Api.Models;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Tests;

public sealed class AlertEmailTemplateRendererTests
{
    [Fact]
    public void Render_BuildsHtmlEmailAndPlainTextFallback()
    {
        var renderer = new AlertEmailTemplateRenderer();
        var now = new DateTimeOffset(2026, 6, 18, 10, 42, 0, TimeSpan.Zero);
        var analysis = new PostAnalysis
        {
            Id = 42,
            PostId = 7,
            MarketImpactScore = 84,
            Direction = -30,
            Confidence = 92,
            Reasoning = "Tariff mention could pressure equities.",
            AffectedAssetsJson = """["US equities","USD"]""",
            AnalyzerVersion = "test-v1",
            AnalyzedAt = now,
            CreatedAt = now,
            Post = new TruthPost
            {
                Id = 7,
                Source = "truth_social",
                Author = "realDonaldTrump",
                ExternalId = "abc",
                Url = "https://truthsocial.com/@realDonaldTrump/posts/abc",
                Content = "Tariffs <markets> & manufacturing.",
                CreatedAt = now.AddMinutes(-2),
                CollectedAt = now,
                SavedAtUtc = now
            }
        };

        var message = renderer.Render(
            new AlertSettings
            {
                Recipient = "recipient@example.com",
                AlertType = "MarketImpact",
                Threshold = 70
            },
            analysis);

        Assert.Equal("recipient@example.com", message.Recipient);
        Assert.Equal("Trump fibblar med marknaden!", message.Subject);
        Assert.Contains("Market Impact Score: 84/100", message.Body);
        Assert.Contains("Direction: Bearish (-30 / -50)", message.Body);
        Assert.Contains("Direction range: -50 to +50", message.Body);
        Assert.Contains("Confidence: 92/100", message.Body);
        Assert.Contains("Alert criteria: score > 60, confidence > 20, direction outside -4 to +4", message.Body);
        Assert.Contains("Affected assets: US equities, USD", message.Body);
        Assert.NotNull(message.HtmlBody);
        Assert.Contains("TrumpStockAlert", message.HtmlBody);
        Assert.Contains("Real-time Truth Social", message.HtmlBody);
        Assert.Contains("Bearish -30 / -50", message.HtmlBody);
        Assert.Contains("Direction range: -50 to +50", message.HtmlBody);
        Assert.Contains("92 / 100", message.HtmlBody);
        Assert.Contains("Tariffs &lt;markets&gt; &amp; manufacturing.", message.HtmlBody);
        Assert.Contains("US equities", message.HtmlBody);
        Assert.Contains("https://truthsocial.com/@realDonaldTrump/posts/abc", message.HtmlBody);
    }

    [Fact]
    public void Render_HandlesMissingOptionalValuesGracefully()
    {
        var renderer = new AlertEmailTemplateRenderer();
        var analysis = new PostAnalysis
        {
            Id = 1,
            PostId = 2,
            MarketImpactScore = 50,
            Direction = 0,
            Confidence = 50,
            Reasoning = "",
            AffectedAssetsJson = "not-json",
            AnalyzerVersion = "",
            AnalyzedAt = default,
            CreatedAt = default,
            Post = new TruthPost
            {
                Id = 2,
                Source = "truth_social",
                Author = "",
                ExternalId = "missing",
                Url = "",
                Content = "",
                CreatedAt = default,
                CollectedAt = default,
                SavedAtUtc = default
            }
        };

        var message = renderer.Render(
            new AlertSettings { Recipient = "recipient@example.com" },
            analysis);

        Assert.Equal("Trump fibblar med marknaden!", message.Subject);
        Assert.Contains("Direction: Neutral (0 / +/-50)", message.Body);
        Assert.Contains("Confidence: 50/100", message.Body);
        Assert.Contains("No AI reasoning was provided.", message.Body);
        Assert.Contains("Original post text is unavailable.", message.Body);
        Assert.DoesNotContain("View source post:", message.Body);
        Assert.DoesNotContain("View Full Source", message.HtmlBody);
        Assert.Contains("0 / &#x2B;/-50", message.HtmlBody);
        Assert.Contains("50 / 100", message.HtmlBody);
        Assert.Contains("Unavailable", message.HtmlBody);
    }
}
