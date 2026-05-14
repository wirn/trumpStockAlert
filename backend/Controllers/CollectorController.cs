using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using TrumpStockAlert.Api.DTOs;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Controllers;

[ApiController]
[Route("api/collector")]
public sealed class CollectorController(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ICollectorRunner collectorRunner,
    ICollectorTestRunner collectorTestRunner,
    IFetcherRunService fetcherRunService,
    ILogger<CollectorController> logger) : ControllerBase
{
    private const string SchedulerKeyHeaderName = "X-TrumpStockAlert-Scheduler-Key";
    private const string TriggerTypeHeaderName = "X-TrumpStockAlert-Trigger-Type";
    private const string SchedulerApiKeyConfigurationName = "Scheduler:ApiKey";

    /// <summary>
    /// Runs the production collector.
    /// </summary>
    /// <remarks>
    /// Intended for manual execution and future scheduled execution.
    /// Requires the <c>X-TrumpStockAlert-Scheduler-Key</c> request header matching <c>Scheduler:ApiKey</c>.
    /// This endpoint only fetches Truth Social posts and saves new rows; it does not run AI analysis.
    /// </remarks>
    [HttpPost("run")]
    [ProducesResponseType(typeof(CollectorRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CollectorRunResponse>> RunCollector(
        [FromHeader(Name = SchedulerKeyHeaderName)] string? schedulerKey,
        [FromHeader(Name = TriggerTypeHeaderName)] string? triggerTypeHeader,
        CancellationToken cancellationToken)
    {
        var triggerType = GetTriggerType(triggerTypeHeader);
        var requestStartedAt = DateTimeOffset.UtcNow;
        logger.LogInformation(
            "Collector run request received. TriggerType: {TriggerType}.",
            triggerType);

        if (!AuthorizeCollectorRun(schedulerKey))
        {
            return Unauthorized();
        }

        try
        {
            var result = await collectorRunner.RunAsync(cancellationToken);
            var response = CollectorRunResponse.FromResult(result);
            await TryLogFetcherRunAsync(triggerType, result, cancellationToken);

            if (response.Success)
            {
                logger.LogInformation(
                    "Collector run completed successfully. TriggerType: {TriggerType}. FetchedCount: {FetchedCount}. InsertedCount: {InsertedCount}. DuplicateCount: {DuplicateCount}. ErrorCount: {ErrorCount}. DurationMs: {DurationMs}.",
                    triggerType,
                    response.FetchedCount,
                    response.InsertedCount,
                    response.DuplicateCount,
                    response.ErrorCount,
                    response.DurationMs);

                return Ok(response);
            }

            logger.LogWarning(
                "Collector run failed. TriggerType: {TriggerType}. FetchedCount: {FetchedCount}. InsertedCount: {InsertedCount}. DuplicateCount: {DuplicateCount}. ErrorCount: {ErrorCount}. DurationMs: {DurationMs}. Message: {Message}",
                triggerType,
                response.FetchedCount,
                response.InsertedCount,
                response.DuplicateCount,
                response.ErrorCount,
                response.DurationMs,
                response.Message);

            return StatusCode(StatusCodes.Status500InternalServerError, response);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Collector run failed before completion. TriggerType: {TriggerType}.",
                triggerType);
            await TryLogFetcherFailureAsync(
                triggerType,
                requestStartedAt,
                exception.Message,
                cancellationToken);
            return Problem(
                title: "Collector run failed.",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Runs the collector in test mode.
    /// </summary>
    /// <remarks>
    /// Fetches a one Truth Social post and stores it in the database.
    /// This endpoint is intended for local development/testing only.
    /// </remarks>
    [HttpPost("run-test")]
    [ProducesResponseType(typeof(CollectorRunTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CollectorRunTestResponse>> RunCollectorTestMode(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await RunCollectorTestCore(cancellationToken);
            return result is null
                ? NotFound()
                : Ok(CollectorRunTestResponse.FromResult(result));
        }
        catch (FileNotFoundException exception)
        {
            return Problem(
                title: "Collector test script not found.",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Problem(
                title: "Collector test run failed.",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private async Task<CollectorTestRunResult?> RunCollectorTestCore(
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            logger.LogWarning("Collector test run was requested outside Development and was rejected.");
            return null;
        }

        try
        {
            return await collectorTestRunner.RunTestAsync(cancellationToken);
        }
        catch (FileNotFoundException exception)
        {
            logger.LogError(exception, "Collector test script was not found.");
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Collector test run failed before completion.");
            throw;
        }
    }

    private bool AuthorizeCollectorRun(string? apiKey)
    {
        var configuredApiKey = configuration[SchedulerApiKeyConfigurationName];
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            logger.LogError(
                "Manual collector run was rejected because {ConfigurationName} is not configured.",
                SchedulerApiKeyConfigurationName);
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Manual collector run was rejected because the scheduler key header is missing.");
            return false;
        }

        if (!ApiKeysMatch(configuredApiKey, apiKey))
        {
            logger.LogWarning("Manual collector run was rejected because the scheduler key was invalid.");
            return false;
        }

        return true;
    }

    private static bool ApiKeysMatch(string configuredApiKey, string providedApiKey)
    {
        var configuredBytes = Encoding.UTF8.GetBytes(configuredApiKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedApiKey);
        return configuredBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
    }

    private static string GetTriggerType(string? triggerTypeHeader)
    {
        return string.Equals(triggerTypeHeader, FetcherRunTriggerType.Scheduler, StringComparison.OrdinalIgnoreCase)
            ? FetcherRunTriggerType.Scheduler
            : FetcherRunTriggerType.Manual;
    }

    private async Task TryLogFetcherRunAsync(
        string triggerType,
        CollectorRunResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            await fetcherRunService.LogRunAsync(triggerType, result, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Failed to persist FetcherRun for collector run. TriggerType: {TriggerType}.",
                triggerType);
        }
    }

    private async Task TryLogFetcherFailureAsync(
        string triggerType,
        DateTimeOffset startedAt,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await fetcherRunService.LogFailureAsync(triggerType, startedAt, message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Failed to persist failed FetcherRun. TriggerType: {TriggerType}.",
                triggerType);
        }
    }
}
