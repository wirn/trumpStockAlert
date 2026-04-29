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
    ICollectorRunner collectorRunner,
    ICollectorTestRunner collectorTestRunner,
    ILogger<CollectorController> logger) : ControllerBase
{
    private const string ApiKeyHeaderName = "x-api-key";
    private const string ApiKeyEnvironmentVariableName = "Collector__ApiKey";

    /// <summary>
    /// Runs the production collector.
    /// </summary>
    /// <remarks>
    /// Intended for scheduled execution, such as an Azure Function Timer Trigger.
    /// Requires the <c>x-api-key</c> request header matching <c>Collector__ApiKey</c>.
    /// This endpoint only fetches Truth Social posts and saves new rows; it does not run AI analysis.
    /// </remarks>
    [HttpPost("run")]
    [ProducesResponseType(typeof(CollectorRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CollectorRunResponse>> RunCollector(
        [FromHeader(Name = ApiKeyHeaderName)] string? apiKey,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Collector run request received.");

        if (!AuthorizeCollectorRun(apiKey))
        {
            return Unauthorized();
        }

        try
        {
            var result = await collectorRunner.RunAsync(cancellationToken);
            var response = CollectorRunResponse.FromResult(result);

            if (response.Success)
            {
                logger.LogInformation(
                    "Collector run request completed successfully. FetchedPosts: {FetchedPosts}. SavedPosts: {SavedPosts}. SkippedPosts: {SkippedPosts}.",
                    response.FetchedPosts,
                    response.SavedPosts,
                    response.SkippedPosts);
            }
            else
            {
                logger.LogWarning(
                    "Collector run request completed with failures. FetchedPosts: {FetchedPosts}. SavedPosts: {SavedPosts}. SkippedPosts: {SkippedPosts}. Message: {Message}",
                    response.FetchedPosts,
                    response.SavedPosts,
                    response.SkippedPosts,
                    response.Message);
            }

            return Ok(response);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Production collector run failed before completion.");
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
        var configuredApiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            logger.LogError(
                "Collector run was rejected because {EnvironmentVariableName} is not configured.",
                ApiKeyEnvironmentVariableName);
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Collector run was rejected because the API key header is missing.");
            return false;
        }

        if (!ApiKeysMatch(configuredApiKey, apiKey))
        {
            logger.LogWarning("Collector run was rejected because the API key was invalid.");
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
}
