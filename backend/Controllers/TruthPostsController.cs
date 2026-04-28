using Microsoft.AspNetCore.Mvc;
using TrumpStockAlert.Api.DTOs;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Controllers;

[ApiController]
/// <summary>
/// Returns collected Truth Social posts.
/// </summary>
/// <remarks>
/// Used to verify that the collector has saved posts. Each post may include its latest analysis result if available.
/// </remarks>
[Route("api/truth-posts")]
public sealed class TruthPostsController(
    ITruthPostService truthPostService,
    ILogger<TruthPostsController> logger) : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 500;

    [HttpPost]
    [ProducesResponseType(typeof(TruthPostResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(TruthPostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TruthPostResponse>> Create(
        [FromBody] CreateTruthPostRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasRequiredText(request.Source)
            || !HasRequiredText(request.Author)
            || !HasRequiredText(request.ExternalId)
            || !HasRequiredText(request.Url)
            || !HasRequiredText(request.Content))
        {
            return BadRequest("source, author, externalId, url, and content are required.");
        }

        if (request.CreatedAt == default || request.CollectedAt == default)
        {
            return BadRequest("createdAt and collectedAt are required.");
        }

        var result = await truthPostService.SaveAsync(request, cancellationToken);
        var response = TruthPostResponse.FromEntity(result.Post);

        if (!result.Created)
        {
            return Ok(response);
        }

        logger.LogInformation("Created truth post {Id}.", response.Id);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TruthPostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TruthPostResponse>>> GetLatest(
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = limit ?? DefaultLimit;
        if (effectiveLimit < 1 || effectiveLimit > MaxLimit)
        {
            return BadRequest($"limit must be between 1 and {MaxLimit}.");
        }

        var posts = await truthPostService.GetLatestAsync(effectiveLimit, cancellationToken);
        return Ok(posts.Select(TruthPostResponse.FromEntity).ToList());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TruthPostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TruthPostResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var post = await truthPostService.GetByIdAsync(id, cancellationToken);
        if (post is null)
        {
            return NotFound();
        }

        return Ok(TruthPostResponse.FromEntity(post));
    }

    private static bool HasRequiredText(string value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }
}
