using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TrumpStockAlert.Api.DTOs;

namespace TrumpStockAlert.Api.Controllers;

[ApiController]
[Route("api/collector")]
public sealed class CollectorController(
    IWebHostEnvironment environment,
    ILogger<CollectorController> logger) : ControllerBase
{
    private static readonly TimeSpan CollectorTimeout = TimeSpan.FromSeconds(60);

    [HttpPost("test-run")]
    [ProducesResponseType(typeof(CollectorTestRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CollectorTestRunResponse>> RunCollectorTest(
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var scriptPath = Path.GetFullPath(
            Path.Combine(environment.ContentRootPath, "..", "run-collector.ps1"));

        if (!System.IO.File.Exists(scriptPath))
        {
            logger.LogError("Collector test script was not found at {ScriptPath}.", scriptPath);
            return Problem(
                title: "Collector test script not found.",
                detail: $"Expected script at: {scriptPath}",
                statusCode: StatusCodes.Status500InternalServerError);
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

        try
        {
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

            if (success)
            {
                logger.LogInformation("Collector test run completed successfully.");
            }
            else
            {
                logger.LogError(
                    "Collector test run failed. ExitCode: {ExitCode}. TimedOut: {TimedOut}.",
                    exitCode,
                    timedOut);
            }

            return Ok(new CollectorTestRunResponse
            {
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                ExitCode = exitCode,
                Success = success,
                TimedOut = timedOut,
                Stdout = stdout,
                Stderr = stderr
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Collector test run failed before completion.");
            return Problem(
                title: "Collector test run failed.",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
