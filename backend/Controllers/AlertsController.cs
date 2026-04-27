using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.DTOs;

namespace TrumpStockAlert.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController(AppDbContext dbContext) : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 500;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AlertResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AlertResponse>>> GetLatest(
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = limit ?? DefaultLimit;
        if (effectiveLimit < 1 || effectiveLimit > MaxLimit)
        {
            return BadRequest($"limit must be between 1 and {MaxLimit}.");
        }

        var alerts = await dbContext.Alerts
            .AsNoTracking()
            .OrderByDescending(alert => alert.CreatedAt)
            .Take(effectiveLimit)
            .ToListAsync(cancellationToken);

        return Ok(alerts.Select(AlertResponse.FromEntity).ToList());
    }
}
