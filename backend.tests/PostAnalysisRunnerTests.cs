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

    private PostAnalysisRunner CreateRunner(IMarketImpactAnalyzer analyzer)
    {
        return new PostAnalysisRunner(_db, analyzer, NullLogger<PostAnalysisRunner>.Instance);
    }

    [Fact]
    public async Task AnalyzePendingPostsAsync_NoPosts_ReturnsZeroCounts()
    {
        var result = await _runner.AnalyzePendingPostsAsync();

        Assert.Equal(0, result.AnalyzedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.SkippedAlreadyAnalyzedCount);
        Assert.Equal(0, result.SkippedNoTextContentCount);
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
        Assert.Equal(-25, analysis.Direction);
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
        Assert.Equal(1, secondResult.SkippedAlreadyAnalyzedCount);
        Assert.Equal(0, secondResult.SkippedNoTextContentCount);

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
    public async Task AnalyzePendingPostsAsync_PlaceholderOnlyPost_IsSkipped()
    {
        var post = MakePost("[No text content]");
        _db.TruthPosts.Add(post);
        await _db.SaveChangesAsync();

        var result = await _runner.AnalyzePendingPostsAsync();

        Assert.Equal(0, result.AnalyzedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, result.SkippedAlreadyAnalyzedCount);
        Assert.Equal(1, result.SkippedNoTextContentCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(result.AnalyzedPostIds);
        Assert.Equal(0, await _db.PostAnalyses.CountAsync());
    }

    [Fact]
    public async Task AnalyzePendingPostsAsync_MixedBatch_SkipsPlaceholderAndAnalyzesRealContent()
    {
        var realPost = MakePost("tariffs on China");
        var placeholderPost = MakePost("[No text content]");
        _db.TruthPosts.AddRange(realPost, placeholderPost);
        await _db.SaveChangesAsync();

        var result = await _runner.AnalyzePendingPostsAsync();

        Assert.Equal(1, result.AnalyzedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, result.SkippedAlreadyAnalyzedCount);
        Assert.Equal(1, result.SkippedNoTextContentCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Contains(realPost.Id, result.AnalyzedPostIds);
        Assert.DoesNotContain(placeholderPost.Id, result.AnalyzedPostIds);
        Assert.Equal(1, await _db.PostAnalyses.CountAsync());
        Assert.NotNull(await _db.PostAnalyses.SingleOrDefaultAsync(a => a.PostId == realPost.Id));
        Assert.Null(await _db.PostAnalyses.SingleOrDefaultAsync(a => a.PostId == placeholderPost.Id));
    }

    [Fact]
    public async Task AnalyzePendingPostsAsync_DoesNotInvokeAnalyzerForPlaceholderPost()
    {
        var analyzer = new CountingAnalyzer();
        var runner = CreateRunner(analyzer);
        _db.TruthPosts.Add(MakePost("[No text content]"));
        await _db.SaveChangesAsync();

        var result = await runner.AnalyzePendingPostsAsync();

        Assert.Equal(0, result.AnalyzedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, result.SkippedAlreadyAnalyzedCount);
        Assert.Equal(1, result.SkippedNoTextContentCount);
        Assert.Equal(0, analyzer.CallCount);
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
        Assert.Equal(1, result.SkippedAlreadyAnalyzedCount);
        Assert.Equal(0, result.SkippedNoTextContentCount);
    }

    [Fact]
    public async Task AnalyzePendingPostsAsync_Rerun_DedupesAnalyzedPostsAndStillSkipsPlaceholder()
    {
        var realPost = MakePost("tariffs on China");
        var placeholderPost = MakePost("[No text content]");
        _db.TruthPosts.AddRange(realPost, placeholderPost);
        await _db.SaveChangesAsync();

        await _runner.AnalyzePendingPostsAsync();
        var secondResult = await _runner.AnalyzePendingPostsAsync();

        Assert.Equal(0, secondResult.AnalyzedCount);
        Assert.Equal(2, secondResult.SkippedCount);
        Assert.Equal(1, secondResult.SkippedAlreadyAnalyzedCount);
        Assert.Equal(1, secondResult.SkippedNoTextContentCount);
        Assert.Equal(0, secondResult.FailedCount);
        Assert.Equal(1, await _db.PostAnalyses.CountAsync());
        Assert.NotNull(await _db.PostAnalyses.SingleOrDefaultAsync(a => a.PostId == realPost.Id));
        Assert.Null(await _db.PostAnalyses.SingleOrDefaultAsync(a => a.PostId == placeholderPost.Id));
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

    private sealed class CountingAnalyzer : IMarketImpactAnalyzer
    {
        private readonly MockMarketImpactAnalyzer _inner = new();

        public int CallCount { get; private set; }

        public Task<MarketImpactAnalysisResult> AnalyzeAsync(
            TruthPost post,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return _inner.AnalyzeAsync(post, cancellationToken);
        }
    }
}
