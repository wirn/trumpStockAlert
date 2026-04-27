namespace TrumpStockAlert.Api.Services;

public interface IPostAnalysisRunner
{
    Task<PostAnalysisRunResult> AnalyzePendingPostsAsync(
        CancellationToken cancellationToken = default);
}
