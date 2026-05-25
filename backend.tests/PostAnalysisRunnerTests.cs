using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.Models;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Tests;

public sealed class PostAnalysisRunnerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly MockMarketImpactAnalyzer _analyzer = new();
    private readonly PostAnalysisRunner _runner;

    public PostAnalysisRunnerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _runner = new PostAnalysisRunner(_db, _analyzer, NullLogger<PostAnalysisRunner>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static TruthPost MakePost(string content = "tariffs on China")
    {
        var now = DateTimeOffset.UtcNow;
        return new TruthPost
        {
            Source = "truth_social",
            Author = "realDonaldTrump",
            ExternalId = Guid.NewGuid().ToString(),
            Url = "https://example.com/post/1",
            Content = content,
            CreatedAt = now,
            CollectedAt = now,
            SavedAtUtc = now
        };
    }

    [Fact]
    public async Task AnalyzePendingPostsAsync_NoPosts_ReturnsZeroCounts()
    {
        var result = await _runner.AnalyzePendingPostsAsync();

        Assert.Equal(0, result.AnalyzedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task AnalyzePendingPostsAsync_OnePost_PersistsAnalysis()
    {
        var post = MakePost();
        _db.TruthPosts.Add(post);
        await _db.SaveChangesAsync();

        var result = await _runner.AnalyzePendingPostsAsync();

        Assert.Equal(1, result.AnalyzedCount);
        Assert.Equal(0, result.FailedCount);

        var analysis = await _db.PostAnalyses.SingleOrDefaultAsync(a => a.PostId == post.Id);
        Assert.NotNull(analysis);
        Assert.InRange(analysis.MarketImpactScore, 1, 100);
        Assert.InRange(analysis.Direction, -50, 50);
        Assert.InRange(analysis.Confidence, 1, 100);
        Assert.False(string.IsNullOrWhiteSpace(analysis.Reasoning));
        Assert.False(string.IsNullOrWhiteSpace(analysis.AnalyzerVersion));
        Assert.True(analysis.AnalyzedAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task AnalyzePendingPostsAsync_PostAlreadyAnalyzed_IsSkipped()
    {
        var post = MakePost();
        _db.TruthPosts.Add(post);
        await _db.SaveChangesAsync();

        await _runner.AnalyzePendingPostsAsync();

        var secondResult = await _runner.AnalyzePendingPostsAsync();

        Assert.Equal(0, secondResult.AnalyzedCount);
        Assert.Equal(1, secondResult.SkippedCount);

        var analysisCount = await _db.PostAnalyses.CountAsync(a => a.PostId == post.Id);
        Assert.Equal(1, analysisCount);
    }

    [Fact]
    public async Task AnalyzePendingPostsAsync_MultiplePosts_AnalyzesAll()
    {
        for (var i = 0; i < 3; i++)
        {
            _db.TruthPosts.Add(MakePost($"content {i}"));
        }
        await _db.SaveChangesAsync();

        var result = await _runner.AnalyzePendingPostsAsync();

        Assert.Equal(3, result.AnalyzedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(3, await _db.PostAnalyses.CountAsync());
    }

    [Fact]
    public async Task AnalyzePendingPostsAsync_MixedPendingAndAnalyzed_OnlyAnalyzesPending()
    {
        var analyzed = MakePost("already analyzed");
        var pending = MakePost("pending post");
        _db.TruthPosts.AddRange(analyzed, pending);
        await _db.SaveChangesAsync();

        await _runner.AnalyzePendingPostsAsync();

        pending = await _db.TruthPosts.SingleAsync(p => p.ExternalId == pending.ExternalId);
        _db.PostAnalyses.Remove(await _db.PostAnalyses.SingleAsync(a => a.PostId == pending.Id));
        await _db.SaveChangesAsync();

        var result = await _runner.AnalyzePendingPostsAsync();

        Assert.Equal(1, result.AnalyzedCount);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public async Task AnalyzePendingPostsAsync_AnalyzedPostIds_ContainsCorrectIds()
    {
        var post = MakePost();
        _db.TruthPosts.Add(post);
        await _db.SaveChangesAsync();

        var result = await _runner.AnalyzePendingPostsAsync();

        Assert.Contains(post.Id, result.AnalyzedPostIds);
    }
}
