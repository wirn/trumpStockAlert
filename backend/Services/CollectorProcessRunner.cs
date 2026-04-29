using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TrumpStockAlert.Api.Services;

public sealed partial class CollectorProcessRunner(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<CollectorProcessRunner> logger) : ICollectorProcessRunner
{
    private static readonly TimeSpan CollectorTimeout = TimeSpan.FromSeconds(60);

    public async Task<CollectorProcessRunResult> RunAsync(
        bool testMode,
        CancellationToken cancellationToken)
    {
        var scriptPath = ResolveScriptPath();

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Collector script was not found.", scriptPath);
        }

        var startedAt = DateTimeOffset.UtcNow;
        var modeName = testMode ? "test" : "production";
        logger.LogInformation(
            "Starting collector {ModeName} run using script {ScriptPath}.",
            modeName,
            scriptPath);

        using var process = new Process
        {
            StartInfo = CreateStartInfo(scriptPath, testMode),
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
                "Collector {ModeName} run timed out after {TimeoutSeconds} seconds.",
                modeName,
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
        var fetchedPosts = ParseCount(KeptPostsRegex(), stdout, stderr)
            ?? ParseCount(FetchedPostsRegex(), stdout, stderr);
        var savedPosts = ParseCount(SavedPostsRegex(), stdout, stderr);
        var skippedPosts = ParseCount(SkippedPostsRegex(), stdout, stderr)
            ?? CalculateSkippedPosts(fetchedPosts, savedPosts);
        var message = CreateMessage(modeName, success, timedOut, exitCode, fetchedPosts, savedPosts, skippedPosts);

        if (success)
        {
            logger.LogInformation(
                "Collector {ModeName} run completed. FetchedPosts: {FetchedPosts}. SavedPosts: {SavedPosts}. SkippedPosts: {SkippedPosts}.",
                modeName,
                fetchedPosts,
                savedPosts,
                skippedPosts);
        }
        else
        {
            logger.LogError(
                "Collector {ModeName} run failed. ExitCode: {ExitCode}. TimedOut: {TimedOut}. Stderr: {Stderr}",
                modeName,
                exitCode,
                timedOut,
                Truncate(stderr, 2000));
        }

        return new CollectorProcessRunResult
        {
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            ExitCode = exitCode,
            Success = success,
            TimedOut = timedOut,
            FetchedPosts = fetchedPosts,
            SavedPosts = savedPosts,
            SkippedPosts = skippedPosts,
            Message = message,
            Stdout = stdout,
            Stderr = stderr
        };
    }

    private ProcessStartInfo CreateStartInfo(string scriptPath, bool testMode)
    {
        var arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
        if (testMode)
        {
            arguments += " -Test";
        }
        else
        {
            arguments += " -SkipLookback";
        }

        var powerShellExecutable = ResolvePowerShellExecutable();
        logger.LogInformation(
            "Collector process command: {PowerShellExecutable} {Arguments}",
            powerShellExecutable,
            arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = powerShellExecutable,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? environment.ContentRootPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.Environment["COLLECTOR_STORE_MODE"] = "api";

        var backendBaseUrl = ResolveBackendBaseUrl();
        if (!string.IsNullOrWhiteSpace(backendBaseUrl))
        {
            startInfo.Environment["TRUTH_POST_API_BASE_URL"] = backendBaseUrl.TrimEnd('/');
        }

        var pythonExecutable = configuration["Collector:PythonExecutable"]?.Trim();
        if (!string.IsNullOrWhiteSpace(pythonExecutable))
        {
            startInfo.Environment["PYTHON_EXECUTABLE"] = pythonExecutable;
        }

        return startInfo;
    }

    private string ResolvePowerShellExecutable()
    {
        var configuredExecutable = configuration["Collector:PowerShellExecutable"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configuredExecutable))
        {
            return configuredExecutable;
        }

        return OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh";
    }

    private string ResolveScriptPath()
    {
        var configuredScriptPath = configuration["Collector:ScriptPath"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configuredScriptPath))
        {
            return Path.GetFullPath(configuredScriptPath);
        }

        var publishedPath = Path.Combine(environment.ContentRootPath, "run-collector.ps1");
        if (File.Exists(publishedPath))
        {
            return Path.GetFullPath(publishedPath);
        }

        return Path.GetFullPath(
            Path.Combine(environment.ContentRootPath, "..", "run-collector.ps1"));
    }

    private string? ResolveBackendBaseUrl()
    {
        var configuredBaseUrl = configuration["Collector:BackendBaseUrl"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl;
        }

        var websiteHostname = configuration["WEBSITE_HOSTNAME"]?.Trim();
        return string.IsNullOrWhiteSpace(websiteHostname)
            ? null
            : $"https://{websiteHostname}";
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

    private static string CreateMessage(
        string modeName,
        bool success,
        bool timedOut,
        int exitCode,
        int? fetchedPosts,
        int? savedPosts,
        int? skippedPosts)
    {
        if (timedOut)
        {
            return $"Collector {modeName} run timed out before completing.";
        }

        if (!success)
        {
            return $"Collector {modeName} run failed with exit code {exitCode}.";
        }

        var fetched = fetchedPosts.HasValue ? fetchedPosts.Value.ToString() : "an unknown number of";
        var saved = savedPosts.HasValue ? savedPosts.Value.ToString() : "an unknown number of";
        var skipped = skippedPosts.HasValue ? skippedPosts.Value.ToString() : "an unknown number of";
        return $"Collector {modeName} run completed. Fetched {fetched} post(s), saved {saved}, and skipped {skipped}.";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
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
