using Microsoft.AspNetCore.Mvc;
using TrumpStockAlert.Api.DTOs;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Controllers;

[ApiController]
[Route("api/collector")]
public sealed class CollectorController(
    IWebHostEnvironment environment,
    ICollectorTestRunner collectorTestRunner,
    ILogger<CollectorController> logger) : ControllerBase
{
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
}
