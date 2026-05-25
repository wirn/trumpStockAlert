using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TrumpStockAlert.Api.Controllers;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.DTOs;
using TrumpStockAlert.Api.Models;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Tests;

/// <summary>
/// Controller-level tests for POST /api/analyses/run and GET /api/analyses/latest.
/// Each test gets its own in-memory database for full isolation.
/// </summary>
public sealed class AnalysesControllerTests : IDisposable
{
    private const string ValidKey = "test-scheduler-key-abc";
    private const string WrongKey = "wrong-key";

    private readonly AppDbContext _db;
    private readonly PostAnalysisRunner _runner;

    public AnalysesControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _runner = new PostAnalysisRunner(
            _db,
            new MockMarketImpactAnalyzer(),
            NullLogger<PostAnalysisRunner>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private AnalysesController Controller(string? configuredKey = ValidKey)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configuredKey is not null
                ? new Dictionary<string, string?> { ["Scheduler:ApiKey"] = configuredKey }
                : new Dictionary<string, string?>())
            .Build();

        return new AnalysesController(
            _db,
            _runner,
            config,
            NullLogger<AnalysesController>.Instance);
    }

    private async Task SeedPostAsync(string content = "tariffs on China now")
    {
        var now = DateTimeOffset.UtcNow;
        _db.TruthPosts.Add(new TruthPost
        {
            Source = "truth_social",
            Author = "realDonaldTrump",
            ExternalId = Guid.NewGuid().ToString(),
            Url = "https://truthsocial.com/post/1",
            Content = content,
            CreatedAt = now,
            CollectedAt = now,
            SavedAtUtc = now,
        });
        await _db.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------
    // POST /api/analyses/run — auth
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAnalysis_MissingKey_Returns401()
    {
        var result = await Controller().RunAnalysis(null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task RunAnalysis_WrongKey_Returns401()
    {
        var result = await Controller().RunAnalysis(WrongKey, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task RunAnalysis_KeyMismatch_Returns401()
    {
        // Extra check: valid-length but wrong value should still be rejected.
        var result = await Controller().RunAnalysis(ValidKey + "x", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task RunAnalysis_NoConfiguredKey_Returns401()
    {
        // If Scheduler:ApiKey is not configured, every request is rejected.
        var result = await Controller(configuredKey: null).RunAnalysis(ValidKey, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    // -------------------------------------------------------------------------
    // POST /api/analyses/run — response shape
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAnalysis_ValidKey_ReturnsOkWithCorrectShape()
    {
        var result = await Controller().RunAnalysis(ValidKey, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<AnalysisRunResponse>(ok.Value);
        Assert.True(body.DurationMs >= 0);
        Assert.True(body.FinishedAt >= body.StartedAt);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
    }

    [Fact]
    public async Task RunAnalysis_ValidKey_NoPendingPosts_ReturnsZeroCounts()
    {
        var result = await Controller().RunAnalysis(ValidKey, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<AnalysisRunResponse>(ok.Value);
        Assert.Equal(0, body.AnalyzedCount);
        Assert.Equal(0, body.ErrorCount);
    }

    // -------------------------------------------------------------------------
    // POST /api/analyses/run — logic
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAnalysis_WithPendingPost_ReportsAnalyzedCount()
    {
        await SeedPostAsync("tariffs on China now");

        var result = await Controller().RunAnalysis(ValidKey, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<AnalysisRunResponse>(ok.Value);
        Assert.Equal(1, body.AnalyzedCount);
        Assert.Equal(0, body.ErrorCount);
    }

    [Fact]
    public async Task RunAnalysis_SecondRun_SkipsAlreadyAnalyzedPosts()
    {
        await SeedPostAsync("tariffs on China now");
        var controller = Controller();

        await controller.RunAnalysis(ValidKey, CancellationToken.None);
        var result = await controller.RunAnalysis(ValidKey, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<AnalysisRunResponse>(ok.Value);
        Assert.Equal(0, body.AnalyzedCount);
        Assert.Equal(1, body.SkippedCount);
        Assert.Equal(0, body.ErrorCount);
    }

    [Fact]
    public async Task RunAnalysis_SecondRun_DoesNotCreateDuplicateAnalysis()
    {
        await SeedPostAsync("tariffs on China now");
        var controller = Controller();

        await controller.RunAnalysis(ValidKey, CancellationToken.None);
        await controller.RunAnalysis(ValidKey, CancellationToken.None);

        Assert.Equal(1, await _db.PostAnalyses.CountAsync());
    }

    // -------------------------------------------------------------------------
    // GET /api/analyses/latest
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetLatest_EmptyDb_ReturnsEmptyList()
    {
        var result = await Controller().GetLatest(null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<List<PostAnalysisDetailResponse>>(ok.Value);
        Assert.Empty(body);
    }

    [Fact]
    public async Task GetLatest_AfterRun_ReturnsAnalysisWithPostFields()
    {
        const string content = "tariffs on China now";
        await SeedPostAsync(content);
        var controller = Controller();
        await controller.RunAnalysis(ValidKey, CancellationToken.None);

        var result = await controller.GetLatest(null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<List<PostAnalysisDetailResponse>>(ok.Value);
        Assert.Single(body);
        var detail = body[0];
        Assert.Equal(content, detail.PostContent);
        Assert.False(string.IsNullOrWhiteSpace(detail.PostUrl));
        Assert.InRange(detail.MarketImpactScore, 1, 100);
        Assert.InRange(detail.Direction, -50, 50);
        Assert.InRange(detail.Confidence, 1, 100);
        Assert.False(string.IsNullOrWhiteSpace(detail.Reasoning));
        Assert.False(string.IsNullOrWhiteSpace(detail.AnalyzerVersion));
    }

    [Fact]
    public async Task GetLatest_InvalidLimit_ReturnsBadRequest()
    {
        var result = await Controller().GetLatest(0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetLatest_LimitExceededMax_ReturnsBadRequest()
    {
        var result = await Controller().GetLatest(501, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
