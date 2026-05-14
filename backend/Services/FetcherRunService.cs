using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public sealed class FetcherRunService(
    AppDbContext dbContext,
    ILogger<FetcherRunService> logger) : IFetcherRunService
{
    public async Task LogRunAsync(
        string triggerType,
        CollectorRunResult result,
        CancellationToken cancellationToken)
    {
        var run = new FetcherRun
        {
            StartedAt = result.StartedAt,
            FinishedAt = result.FinishedAt,
            DurationMs = result.DurationMs,
            Status = GetStatus(result),
            TriggerType = NormalizeTriggerType(triggerType),
            FetchedCount = result.FetchedPosts,
            InsertedCount = result.SavedPosts,
            DuplicateCount = result.SkippedPosts,
            ErrorCount = result.FailedPosts,
            Message = result.Message
        };

        await SaveAsync(run, cancellationToken);
    }

    public async Task LogFailureAsync(
        string triggerType,
        DateTimeOffset startedAt,
        string message,
        CancellationToken cancellationToken)
    {
        var finishedAt = DateTimeOffset.UtcNow;
        var run = new FetcherRun
        {
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            DurationMs = (long)(finishedAt - startedAt).TotalMilliseconds,
            Status = FetcherRunStatus.Failed,
            TriggerType = NormalizeTriggerType(triggerType),
            FetchedCount = 0,
            InsertedCount = 0,
            DuplicateCount = 0,
            ErrorCount = 1,
            Message = message
        };

        await SaveAsync(run, cancellationToken);
    }

    private async Task SaveAsync(FetcherRun run, CancellationToken cancellationToken)
    {
        dbContext.FetcherRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Saved FetcherRun {FetcherRunId}. TriggerType: {TriggerType}. Status: {Status}. FetchedCount: {FetchedCount}. InsertedCount: {InsertedCount}. DuplicateCount: {DuplicateCount}. ErrorCount: {ErrorCount}. DurationMs: {DurationMs}.",
            run.Id,
            run.TriggerType,
            run.Status,
            run.FetchedCount,
            run.InsertedCount,
            run.DuplicateCount,
            run.ErrorCount,
            run.DurationMs);
    }

    private static string GetStatus(CollectorRunResult result)
    {
        if (result.Success)
        {
            return FetcherRunStatus.Completed;
        }

        return result.FetchedPosts > 0 || result.SavedPosts > 0 || result.SkippedPosts > 0
            ? FetcherRunStatus.CompletedWithErrors
            : FetcherRunStatus.Failed;
    }

    private static string NormalizeTriggerType(string triggerType)
    {
        return string.Equals(triggerType, FetcherRunTriggerType.Scheduler, StringComparison.OrdinalIgnoreCase)
            ? FetcherRunTriggerType.Scheduler
            : FetcherRunTriggerType.Manual;
    }
}
