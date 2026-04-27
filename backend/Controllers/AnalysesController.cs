using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrumpStockAlert.Api.Data;
using TrumpStockAlert.Api.DTOs;

namespace TrumpStockAlert.Api.Controllers;

[ApiController]
[Route("api/analyses")]
public sealed class AnalysesController(AppDbContext dbContext) : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 500;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PostAnalysisResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PostAnalysisResponse>>> GetLatest(
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = limit ?? DefaultLimit;
        if (effectiveLimit < 1 || effectiveLimit > MaxLimit)
        {
            return BadRequest($"limit must be between 1 and {MaxLimit}.");
        }

        var analyses = await dbContext.PostAnalyses
            .AsNoTracking()
            .OrderByDescending(analysis => analysis.CreatedAt)
            .Take(effectiveLimit)
            .ToListAsync(cancellationToken);

        return Ok(analyses.Select(PostAnalysisResponse.FromEntity).ToList());
    }
}
