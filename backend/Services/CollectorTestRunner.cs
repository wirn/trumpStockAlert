namespace TrumpStockAlert.Api.Services;

public sealed class CollectorTestRunner(
    ICollectorProcessRunner collectorProcessRunner,
    ILogger<CollectorTestRunner> logger) : ICollectorTestRunner
{
    public async Task<CollectorTestRunResult> RunTestAsync(CancellationToken cancellationToken)
    {
        var result = await collectorProcessRunner.RunAsync(testMode: true, cancellationToken);

        if (result.Success)
        {
            logger.LogInformation(
                "Collector test run completed successfully. FetchedPosts: {FetchedPosts}. SavedPosts: {SavedPosts}. SkippedPosts: {SkippedPosts}.",
                result.FetchedPosts,
                result.SavedPosts,
                result.SkippedPosts);
        }
        else
        {
            logger.LogError(
                "Collector test run failed. ExitCode: {ExitCode}. TimedOut: {TimedOut}.",
                result.ExitCode,
                result.TimedOut);
        }

        return new CollectorTestRunResult
        {
            StartedAt = result.StartedAt,
            FinishedAt = result.FinishedAt,
            ExitCode = result.ExitCode,
            Success = result.Success,
            TimedOut = result.TimedOut,
            FetchedPosts = result.FetchedPosts,
            SavedPosts = result.SavedPosts,
            Message = result.Message,
            Stdout = result.Stdout,
            Stderr = result.Stderr
        };
    }
}
