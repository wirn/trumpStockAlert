using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.DTOs;

namespace TrumpStockAlert.Api.Controllers;

[ApiController]
[Route("api/fetcher-runs")]
public sealed class FetcherRunsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("latest")]
    [ProducesResponseType(typeof(IReadOnlyList<FetcherRunResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FetcherRunResponse>>> GetLatest(
        CancellationToken cancellationToken)
    {
        var runs = await dbContext.FetcherRuns
            .AsNoTracking()
            .OrderByDescending(run => run.StartedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        return Ok(runs.Select(FetcherRunResponse.FromEntity).ToList());
    }
}
