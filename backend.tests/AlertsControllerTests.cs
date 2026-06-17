using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrumpStockAlert.Api.Controllers;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.DTOs;
using TrumpStockAlert.Api.Models;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Tests;

public sealed class AlertsControllerTests : IDisposable
{
    private const string ValidKey = "test-scheduler-key-abc";

    private readonly AppDbContext _db;
    private readonly AlertsController _controller;

    public AlertsControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scheduler:ApiKey"] = ValidKey
            })
            .Build();

        var evaluator = new AlertEvaluator(
            _db,
            Options.Create(new AlertSettings
            {
                Enabled = true,
                Threshold = 70,
                Recipient = "log-only@trumpstockalert.local",
                AlertType = "MarketImpact"
            }),
            new LogOnlyEmailSender(NullLogger<LogOnlyEmailSender>.Instance),
            NullLogger<AlertEvaluator>.Instance);

        _controller = new AlertsController(
            _db,
            evaluator,
            configuration,
            NullLogger<AlertsController>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task RunAlerts_MissingKey_Returns401()
    {
        var result = await _controller.RunAlerts(null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task RunAlerts_WithEligibleAnalysis_ReturnsOkAndCreatesListedAlert()
    {
        await SeedAnalysisAsync(marketImpactScore: 90);

        var result = await _controller.RunAlerts(ValidKey, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<AlertRunResponse>(ok.Value);
        Assert.Equal(1, body.EvaluatedAnalysisCount);
        Assert.Equal(1, body.EligibleAnalysisCount);
        Assert.Equal(0, body.BelowThresholdCount);
        Assert.Equal(0, body.DuplicateCount);
        Assert.Equal(1, body.CreatedAlertCount);
        Assert.Equal(1, body.SentCount);
        Assert.Equal(0, body.FailedCount);
        Assert.Equal(70, body.Threshold);
        Assert.Equal("log-only@trumpstockalert.local", body.Recipient);

        var listResult = await _controller.GetLatest(null, CancellationToken.None);
        var listOk = Assert.IsType<OkObjectResult>(listResult.Result);
        var alerts = Assert.IsType<List<AlertResponse>>(listOk.Value);
        var alert = Assert.Single(alerts);
        Assert.Equal("Sent", alert.SendStatus);
        Assert.NotNull(alert.SentAt);
        Assert.Equal(body.CreatedAlertIds.Single(), alert.Id);
        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "Score: 90",
                "Direction: 25",
                "Confidence: 80",
                string.Empty,
                "Test analysis reasoning.",
                string.Empty,
                "Tariffs and market-moving comments.",
                string.Empty,
                "https://truthsocial.com/@realDonaldTrump/posts/1"),
            alert.Body);
        Assert.DoesNotContain("threshold", alert.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Post ID", alert.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Analysis ID", alert.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("URL:", alert.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAlerts_BelowThreshold_DoesNotCreateAlert()
    {
        await SeedAnalysisAsync(marketImpactScore: 69);

        var result = await _controller.RunAlerts(ValidKey, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<AlertRunResponse>(ok.Value);
        Assert.Equal(1, body.EvaluatedAnalysisCount);
        Assert.Equal(0, body.EligibleAnalysisCount);
        Assert.Equal(1, body.BelowThresholdCount);
        Assert.Equal(0, body.CreatedAlertCount);
        Assert.Equal(0, body.SentCount);
        Assert.Equal(0, await _db.Alerts.CountAsync());
    }

    [Fact]
    public async Task RunAlerts_SecondRun_DedupesExistingAlert()
    {
        await SeedAnalysisAsync(marketImpactScore: 90);

        await _controller.RunAlerts(ValidKey, CancellationToken.None);
        var result = await _controller.RunAlerts(ValidKey, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<AlertRunResponse>(ok.Value);
        Assert.Equal(1, body.EvaluatedAnalysisCount);
        Assert.Equal(1, body.EligibleAnalysisCount);
        Assert.Equal(1, body.DuplicateCount);
        Assert.Equal(0, body.CreatedAlertCount);
        Assert.Equal(0, body.SentCount);
        Assert.Equal(1, await _db.Alerts.CountAsync());
    }

    private async Task SeedAnalysisAsync(int marketImpactScore)
    {
        var now = DateTimeOffset.UtcNow;
        var post = new TruthPost
        {
            Source = "truth_social",
            Author = "realDonaldTrump",
            ExternalId = Guid.NewGuid().ToString("N"),
            Url = "https://truthsocial.com/@realDonaldTrump/posts/1",
            Content = "Tariffs and market-moving comments.",
            CreatedAt = now,
            CollectedAt = now,
            SavedAtUtc = now
        };

        _db.TruthPosts.Add(post);
        await _db.SaveChangesAsync();

        _db.PostAnalyses.Add(new PostAnalysis
        {
            PostId = post.Id,
            MarketImpactScore = marketImpactScore,
            Direction = 25,
            Reasoning = "Test analysis reasoning.",
            AffectedAssetsJson = "[]",
            Confidence = 80,
            AnalyzerVersion = "test",
            RawAiResponse = "{}",
            AnalyzedAt = now,
            CreatedAt = now
        });
        await _db.SaveChangesAsync();
    }
}
