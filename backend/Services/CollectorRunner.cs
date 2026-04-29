namespace TrumpStockAlert.Api.Services;

public sealed class CollectorRunner(
    ITruthSocialCollectorClient truthSocialClient,
    ITruthPostService truthPostService,
    IConfiguration configuration,
    ILogger<CollectorRunner> logger) : ICollectorRunner
{
    public async Task<CollectorRunResult> RunAsync(CancellationToken cancellationToken)
    {
        var username = configuration["Collector:TruthSocialUsername"]?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("Collector:TruthSocialUsername is not configured.");
        }

        var maxPosts = configuration.GetValue("Collector:MaxPosts", 10);
        if (maxPosts < 1)
        {
            throw new InvalidOperationException("Collector:MaxPosts must be greater than zero.");
        }

        logger.LogInformation(
            "Production collector run starting via .NET Truth Social client. Username: {Username}. MaxPosts: {MaxPosts}.",
            username,
            maxPosts);

        IReadOnlyList<System.Text.Json.JsonElement> rawPosts;
        try
        {
            rawPosts = await truthSocialClient.FetchLatestPostsAsync(username, maxPosts, cancellationToken);
        }
        catch (TruthSocialCollectorClientException exception)
        {
            logger.LogWarning(
                exception,
                "Production collector run could not fetch Truth Social posts. RequestPath: {RequestPath}. StatusCode: {StatusCode}.",
                exception.RequestPath,
                exception.StatusCode.HasValue ? (int)exception.StatusCode.Value : null);

            return new CollectorRunResult
            {
                Success = false,
                Message = exception.Message,
                FetchedPosts = 0,
                SavedPosts = 0,
                SkippedPosts = 0,
                Timestamp = DateTimeOffset.UtcNow
            };
        }

        logger.LogInformation("Fetched {FetchedPosts} posts from Truth Social.", rawPosts.Count);

        var savedPosts = 0;
        var skippedPosts = 0;
        var failedPosts = 0;
        var collectedAt = DateTimeOffset.UtcNow;

        foreach (var rawPost in rawPosts)
        {
            string externalId;
            try
            {
                var request = TruthSocialPostNormalizer.Normalize(username, rawPost, collectedAt);
                externalId = request.ExternalId;
                var saveResult = await truthPostService.SaveAsync(request, cancellationToken);

                if (saveResult.Created)
                {
                    savedPosts++;
                }
                else
                {
                    skippedPosts++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failedPosts++;
                externalId = TruthSocialPostNormalizer.TryGetExternalId(rawPost) ?? "unknown";
                logger.LogError(
                    exception,
                    "Failed to process Truth Social post {ExternalId}.",
                    externalId);
            }
        }

        var success = failedPosts == 0;

        logger.LogInformation(
            "Production collector run completed. FetchedPosts: {FetchedPosts}. SavedPosts: {SavedPosts}. SkippedPosts: {SkippedPosts}. FailedPosts: {FailedPosts}.",
            rawPosts.Count,
            savedPosts,
            skippedPosts,
            failedPosts);

        return new CollectorRunResult
        {
            Success = success,
            Message = success
                ? "Collector completed."
                : $"Collector completed with {failedPosts} failed post(s).",
            FetchedPosts = rawPosts.Count,
            SavedPosts = savedPosts,
            SkippedPosts = skippedPosts,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
