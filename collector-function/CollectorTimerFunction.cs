using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Timer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public sealed partial class CollectorTimerFunction(
    IConfiguration configuration,
    ILogger<CollectorTimerFunction> logger)
{
    private static readonly TimeSpan CollectorTimeout = TimeSpan.FromSeconds(120);

    [Function(nameof(CollectorTimerFunction))]
    public async Task RunAsync(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Collector timer trigger started at {Timestamp}.", DateTimeOffset.UtcNow);

        var backendBaseUrl = configuration["BackendBaseUrl"]?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(backendBaseUrl))
        {
            logger.LogError("BackendBaseUrl is not configured.");
            return;
        }

        var collectorDirectory = ResolveCollectorDirectory();
        var pythonExecutable = ResolvePythonExecutable();
        var arguments = "-m collector.main --skip-lookback";

        logger.LogInformation(
            "Starting collector process. Python: {PythonExecutable}. CollectorDirectory: {CollectorDirectory}. BackendBaseUrl: {BackendBaseUrl}.",
            pythonExecutable,
            collectorDirectory,
            backendBaseUrl);

        using var process = new Process
        {
            StartInfo = CreateStartInfo(pythonExecutable, arguments, collectorDirectory, backendBaseUrl),
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
                "Collector process timed out after {TimeoutSeconds} seconds.",
                CollectorTimeout.TotalSeconds);

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var exitCode = timedOut ? -1 : process.ExitCode;
        var success = !timedOut && exitCode == 0;

        var fetchedPosts = ParseCount(KeptPostsRegex(), stdout, stderr)
            ?? ParseCount(FetchedPostsRegex(), stdout, stderr);
        var savedPosts = ParseCount(SavedPostsRegex(), stdout, stderr);
        var skippedPosts = ParseCount(SkippedPostsRegex(), stdout, stderr)
            ?? CalculateSkippedPosts(fetchedPosts, savedPosts);

        if (success)
        {
            logger.LogInformation(
                "Collector process completed. FetchedPosts: {FetchedPosts}. SavedPosts: {SavedPosts}. SkippedPosts: {SkippedPosts}. Output: {Output}",
                fetchedPosts,
                savedPosts,
                skippedPosts,
                Truncate(stdout, 2000));
            return;
        }

        logger.LogError(
            "Collector process failed. ExitCode: {ExitCode}. TimedOut: {TimedOut}. Stdout: {Stdout}. Stderr: {Stderr}",
            exitCode,
            timedOut,
            Truncate(stdout, 2000),
            Truncate(stderr, 2000));
    }

    private ProcessStartInfo CreateStartInfo(
        string pythonExecutable,
        string arguments,
        string collectorDirectory,
        string backendBaseUrl)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = arguments,
            WorkingDirectory = collectorDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.Environment["COLLECTOR_STORE_MODE"] = "api";
        startInfo.Environment["TRUTH_POST_API_BASE_URL"] = backendBaseUrl;
        CopyOptionalSetting(startInfo, "TruthSocialUsername", "TRUTH_SOCIAL_USERNAME");
        CopyOptionalSetting(startInfo, "MaxPosts", "MAX_POSTS");
        CopyOptionalSetting(startInfo, "OutputMode", "OUTPUT_MODE");

        return startInfo;
    }

    private void CopyOptionalSetting(
        ProcessStartInfo startInfo,
        string configurationKey,
        string environmentVariableName)
    {
        var value = configuration[configurationKey]?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            startInfo.Environment[environmentVariableName] = value;
        }
    }

    private string ResolveCollectorDirectory()
    {
        var configuredPath = GetConfiguredValue("Collector:CollectorDirectory", "CollectorDirectory");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.Combine(AppContext.BaseDirectory, "collector");
    }

    private string ResolvePythonExecutable()
    {
        var configuredExecutable = GetConfiguredValue("Collector:PythonExecutable", "PythonExecutable");
        return string.IsNullOrWhiteSpace(configuredExecutable)
            ? "python"
            : ResolveExecutablePath(configuredExecutable);
    }

    private string? GetConfiguredValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key]?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string ResolveExecutablePath(string executable)
    {
        return Path.IsPathRooted(executable) || executable.Contains(Path.DirectorySeparatorChar) || executable.Contains(Path.AltDirectorySeparatorChar)
            ? Path.GetFullPath(executable)
            : executable;
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

    private static int? CalculateSkippedPosts(int? fetchedPosts, int? savedPosts)
    {
        if (fetchedPosts is null || savedPosts is null)
        {
            return null;
        }

        return Math.Max(0, fetchedPosts.Value - savedPosts.Value);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }

    [GeneratedRegex(@"Kept\s+(\d+)\s+posts", RegexOptions.IgnoreCase)]
    private static partial Regex KeptPostsRegex();

    [GeneratedRegex(@"Fetched\s+(\d+)\s+posts", RegexOptions.IgnoreCase)]
    private static partial Regex FetchedPostsRegex();

    [GeneratedRegex(@"(\d+)\s+new\s+posts\s+were\s+saved", RegexOptions.IgnoreCase)]
    private static partial Regex SavedPostsRegex();

    [GeneratedRegex(@"(\d+)\s+posts\s+were\s+already\s+in\s+the\s+database", RegexOptions.IgnoreCase)]
    private static partial Regex SkippedPostsRegex();
}
