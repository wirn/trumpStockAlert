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
    private const int CollectorStreamLogMaxLength = 12000;

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

        logger.LogInformation(
            "Collector process stdout: {Stdout}",
            Truncate(stdout, CollectorStreamLogMaxLength));
        logger.LogInformation(
            "Collector process stderr: {Stderr}",
            Truncate(stderr, CollectorStreamLogMaxLength));

        var fetchedPosts = ParseCount(KeptPostsRegex(), stdout, stderr)
            ?? ParseCount(FetchedPostsRegex(), stdout, stderr);
        var savedPosts = ParseCount(SavedPostsRegex(), stdout, stderr);
        var skippedPosts = ParseCount(SkippedPostsRegex(), stdout, stderr)
            ?? CalculateSkippedPosts(fetchedPosts, savedPosts);
        var failedPosts = ParseCount(FailedPostsRegex(), stdout, stderr);

        if (success)
        {
            logger.LogInformation(
                "Collector process completed. FetchedPosts: {FetchedPosts}. SavedPosts: {SavedPosts}. SkippedPosts: {SkippedPosts}. FailedPosts: {FailedPosts}. StdoutLength: {StdoutLength}. StderrLength: {StderrLength}.",
                fetchedPosts,
                savedPosts,
                skippedPosts,
                failedPosts,
                stdout.Length,
                stderr.Length);
            return;
        }

        logger.LogError(
            "Collector process failed. ExitCode: {ExitCode}. TimedOut: {TimedOut}. Stdout: {Stdout}. Stderr: {Stderr}",
            exitCode,
            timedOut,
            Truncate(stdout, CollectorStreamLogMaxLength),
            Truncate(stderr, CollectorStreamLogMaxLength));
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
            return ResolveConfiguredPath(configuredPath);
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
            ? ResolveConfiguredPath(executable)
            : executable;
    }

    private static string ResolveConfiguredPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        foreach (var basePath in EnumerateBasePaths())
        {
            var candidate = Path.GetFullPath(Path.Combine(basePath, path));
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.GetFullPath(Path.Combine(GetFunctionAppRoot(), path));
    }

    private static string GetFunctionAppRoot()
    {
        var scriptRoot = Environment.GetEnvironmentVariable("AzureWebJobsScriptRoot");
        return string.IsNullOrWhiteSpace(scriptRoot)
            ? Directory.GetCurrentDirectory()
            : scriptRoot;
    }

    private static IEnumerable<string> EnumerateBasePaths()
    {
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("AzureWebJobsScriptRoot"),
            Environment.GetEnvironmentVariable("FUNCTIONS_APPLICATION_DIRECTORY"),
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var directory = new DirectoryInfo(Path.GetFullPath(root));
            while (directory is not null)
            {
                yield return directory.FullName;
                directory = directory.Parent;
            }
        }
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

    [GeneratedRegex(@"(\d+)\s+posts\s+failed\s+to\s+save", RegexOptions.IgnoreCase)]
    private static partial Regex FailedPostsRegex();
}
