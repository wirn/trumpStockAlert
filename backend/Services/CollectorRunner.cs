namespace TrumpStockAlert.Api.Services;

public sealed class CollectorRunner(
    ICollectorProcessRunner collectorProcessRunner,
    ILogger<CollectorRunner> logger) : ICollectorRunner
{
    public async Task<CollectorRunResult> RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Production collector run starting via shared Truthbrush collector process.");

        var result = await collectorProcessRunner.RunAsync(testMode: false, cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Message);
        }

        var fetchedPosts = result.FetchedPosts ?? 0;
        var savedPosts = result.SavedPosts ?? 0;
        var skippedPosts = result.SkippedPosts ?? Math.Max(0, fetchedPosts - savedPosts);

        logger.LogInformation(
            "Production collector run completed. FetchedPosts: {FetchedPosts}. SavedPosts: {SavedPosts}. SkippedPosts: {SkippedPosts}.",
            fetchedPosts,
            savedPosts,
            skippedPosts);

        return new CollectorRunResult
        {
            Success = true,
            Message = "Collector completed.",
            FetchedPosts = fetchedPosts,
            SavedPosts = savedPosts,
            SkippedPosts = skippedPosts,
            Timestamp = result.FinishedAt
        };
    }
}
