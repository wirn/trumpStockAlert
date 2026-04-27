using Microsoft.AspNetCore.Mvc;
using TrumpStockAlert.Api.DTOs;
using TrumpStockAlert.Api.Models;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Controllers;

[ApiController]
[Route("api/analysis")]
public sealed class AnalysisController(IMarketImpactAnalyzer analyzer) : ControllerBase
{
    [HttpPost("mock-preview")]
    [ProducesResponseType(typeof(MarketImpactAnalysisResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MarketImpactAnalysisResult>> PreviewMockAnalysis(
        [FromBody] MockAnalysisPreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("content is required.");
        }

        var post = new TruthPost
        {
            Source = "preview",
            Author = "preview",
            ExternalId = Guid.NewGuid().ToString("N"),
            Url = "about:blank",
            Content = request.Content,
            CreatedAt = DateTimeOffset.UtcNow,
            CollectedAt = DateTimeOffset.UtcNow,
            SavedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await analyzer.AnalyzeAsync(post, cancellationToken);
        return Ok(result);
    }
}
