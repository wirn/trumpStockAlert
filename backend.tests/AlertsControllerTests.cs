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
    private readonly CapturingEmailSender _emailSender = new();

    public AlertsControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scheduler:ApiKey"] = ValidKey,
                ["AlertEmailPreview:Enabled"] = "true"
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
            new AlertEmailTemplateRenderer(),
            NullLogger<AlertEvaluator>.Instance);

        _controller = new AlertsController(
            _db,
            evaluator,
            Options.Create(new AlertSettings
            {
                Enabled = true,
                Threshold = 70,
                Recipient = "preview@example.com",
                AlertType = "MarketImpact"
            }),
            new AlertEmailTemplateRenderer(),
            _emailSender,
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
    public async Task SendEmailPreview_Disabled_Returns404WithoutSending()
    {
        var controller = CreatePreviewController(previewEnabled: false);

        var result = await controller.SendEmailPreview(ValidKey, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Null(_emailSender.Message);
    }

    [Fact]
    public async Task SendEmailPreview_MissingKey_Returns401WithoutSending()
    {
        var result = await _controller.SendEmailPreview(null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Null(_emailSender.Message);
    }

    [Fact]
    public async Task SendEmailPreview_WithValidKey_SendsHtmlAndPlainTextPreview()
    {
        var result = await _controller.SendEmailPreview(ValidKey, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<AlertEmailPreviewResponse>(ok.Value);
        Assert.Equal("preview@example.com", body.Recipient);
        Assert.True(body.HtmlBodyPresent);
        Assert.Equal("Alert email preview sent.", body.Message);

        var message = Assert.IsType<AlertEmailMessage>(_emailSender.Message);
        Assert.Equal("preview@example.com", message.Recipient);
        Assert.Contains("score 84", message.Subject);
        Assert.Contains("Market Impact Score: 84/100", message.Body);
        Assert.Contains("Direction: Bullish (+18)", message.Body);
        Assert.Contains("Confidence: 83%", message.Body);
        Assert.Contains("Iran", message.Body);
        Assert.Contains("Strait of Hormuz", message.Body);
        Assert.Contains("Crude oil", message.Body);
        Assert.NotNull(message.HtmlBody);
        Assert.Contains("TrumpStockAlert", message.HtmlBody);
        Assert.Contains("Real-time Truth Social", message.HtmlBody);
        Assert.Contains("Bullish", message.HtmlBody);
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
        Assert.Contains("TrumpStockAlert", alert.Body);
        Assert.Contains("Market Impact Score: 90/100", alert.Body);
        Assert.Contains("Direction: Bullish (+25)", alert.Body);
        Assert.Contains("Confidence: 80%", alert.Body);
        Assert.Contains("Test analysis reasoning.", alert.Body);
        Assert.Contains("Tariffs and market-moving comments.", alert.Body);
        Assert.Contains("View source post: https://truthsocial.com/@realDonaldTrump/posts/1", alert.Body);
        Assert.Contains("Threshold: 70", alert.Body);
        Assert.Contains("Post ID", alert.Body);
        Assert.Contains("Analysis ID", alert.Body);
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

    private AlertsController CreatePreviewController(bool previewEnabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scheduler:ApiKey"] = ValidKey,
                ["AlertEmailPreview:Enabled"] = previewEnabled.ToString()
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
            new AlertEmailTemplateRenderer(),
            NullLogger<AlertEvaluator>.Instance);

        return new AlertsController(
            _db,
            evaluator,
            Options.Create(new AlertSettings
            {
                Enabled = true,
                Threshold = 70,
                Recipient = "preview@example.com",
                AlertType = "MarketImpact"
            }),
            new AlertEmailTemplateRenderer(),
            _emailSender,
            configuration,
            NullLogger<AlertsController>.Instance);
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public AlertEmailMessage? Message { get; private set; }

        public Task SendAsync(AlertEmailMessage message, CancellationToken cancellationToken = default)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }
}
