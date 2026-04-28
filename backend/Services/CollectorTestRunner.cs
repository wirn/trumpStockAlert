using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TrumpStockAlert.Api.Services;

public sealed partial class CollectorTestRunner(
    IWebHostEnvironment environment,
    ILogger<CollectorTestRunner> logger) : ICollectorTestRunner
{
    private static readonly TimeSpan CollectorTimeout = TimeSpan.FromSeconds(60);

    public async Task<CollectorTestRunResult> RunTestAsync(CancellationToken cancellationToken)
    {
        var scriptPath = Path.GetFullPath(
            Path.Combine(environment.ContentRootPath, "..", "run-collector.ps1"));

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Collector test script was not found.", scriptPath);
        }

        var startedAt = DateTimeOffset.UtcNow;
        logger.LogInformation("Starting collector test run using script {ScriptPath}.", scriptPath);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Test",
                WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? environment.ContentRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(CollectorTimeout);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            logger.LogError(
                "Collector test run timed out after {TimeoutSeconds} seconds.",
                CollectorTimeout.TotalSeconds);

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var finishedAt = DateTimeOffset.UtcNow;
        var exitCode = timedOut ? -1 : process.ExitCode;
        var success = !timedOut && exitCode == 0;
        var fetchedPosts = ParseCount(FetchedPostsRegex(), stdout, stderr);
        var savedPosts = ParseCount(SavedPostsRegex(), stdout, stderr);
        var message = CreateMessage(success, timedOut, exitCode, fetchedPosts, savedPosts);

        if (success)
        {
            logger.LogInformation(
                "Collector test run completed successfully. FetchedPosts: {FetchedPosts}. SavedPosts: {SavedPosts}.",
                fetchedPosts,
                savedPosts);
        }
        else
        {
            logger.LogError(
                "Collector test run failed. ExitCode: {ExitCode}. TimedOut: {TimedOut}.",
                exitCode,
                timedOut);
        }

        return new CollectorTestRunResult
        {
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            ExitCode = exitCode,
            Success = success,
            TimedOut = timedOut,
            FetchedPosts = fetchedPosts,
            SavedPosts = savedPosts,
            Message = message,
            Stdout = stdout,
            Stderr = stderr
        };
    }

    private static int? ParseCount(Regex regex, params string[] outputs)
    {
        foreach (var output in outputs)
        {
            var match = regex.Match(output);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
            {
                return count;
            }
        }

        return null;
    }

    private static string CreateMessage(
        bool success,
        bool timedOut,
        int exitCode,
        int? fetchedPosts,
        int? savedPosts)
    {
        if (timedOut)
        {
            return "Collector test run timed out before completing.";
        }

        if (!success)
        {
            return $"Collector test run failed with exit code {exitCode}.";
        }

        var fetched = fetchedPosts.HasValue ? fetchedPosts.Value.ToString() : "an unknown number of";
        var saved = savedPosts.HasValue ? savedPosts.Value.ToString() : "an unknown number of";
        return $"Collector test run completed. Fetched {fetched} post(s) and saved {saved} new post(s).";
    }

    [GeneratedRegex(@"Fetched\s+(\d+)\s+posts", RegexOptions.IgnoreCase)]
    private static partial Regex FetchedPostsRegex();

    [GeneratedRegex(@"(\d+)\s+new\s+posts\s+were\s+saved", RegexOptions.IgnoreCase)]
    private static partial Regex SavedPostsRegex();
}
