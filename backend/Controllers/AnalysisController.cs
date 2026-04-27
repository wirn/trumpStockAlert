using Microsoft.AspNetCore.Mvc;
using TrumpStockAlert.Api.DTOs;
using TrumpStockAlert.Api.Models;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Controllers;

[ApiController]
[Route("api/analysis")]
public sealed class AnalysisController(
    IMarketImpactAnalyzer analyzer,
    OpenAiMarketImpactAnalyzer openAiAnalyzer,
    IPostAnalysisRunner analysisRunner,
    MarketImpactPromptBuilder promptBuilder,
    MarketImpactAiResponseParser responseParser) : ControllerBase
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

    [HttpPost("openai-preview")]
    [ProducesResponseType(typeof(OpenAiAnalysisPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<OpenAiAnalysisPreviewResponse>> PreviewOpenAiAnalysis(
        [FromBody] MockAnalysisPreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("content is required.");
        }

        var post = CreatePreviewPost(request.Content);
        try
        {
            var result = await openAiAnalyzer.AnalyzeAsync(post, cancellationToken);
            return Ok(new OpenAiAnalysisPreviewResponse
            {
                Analysis = result,
                RawAiResponse = result.RawAiResponse
            });
        }
        catch (InvalidOperationException exception)
        {
            var statusCode = exception.Message.Contains("missing", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status502BadGateway;

            return Problem(
                detail: exception.Message,
                statusCode: statusCode,
                title: "OpenAI analysis preview failed.");
        }
    }

    [HttpPost("run")]
    [ProducesResponseType(typeof(PostAnalysisRunResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PostAnalysisRunResult>> RunAnalysis(
        CancellationToken cancellationToken)
    {
        var result = await analysisRunner.AnalyzePendingPostsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("prompt-preview")]
    [ProducesResponseType(typeof(PromptPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<PromptPreviewResponse> PreviewPrompt(
        [FromBody] MockAnalysisPreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("content is required.");
        }

        var post = CreatePreviewPost(request.Content);
        return Ok(new PromptPreviewResponse
        {
            Prompt = promptBuilder.BuildPrompt(post),
            ExampleResponse = promptBuilder.BuildExampleResponse()
        });
    }

    [HttpPost("parse-preview")]
    [ProducesResponseType(typeof(ParsePreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ParsePreviewResponse> PreviewParse(
        [FromBody] ParsePreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RawJson))
        {
            return BadRequest("rawJson is required.");
        }

        try
        {
            var response = responseParser.ParseAndValidate(request.RawJson);
            return Ok(new ParsePreviewResponse
            {
                IsValid = true,
                ParsedResponse = response,
                Error = null
            });
        }
        catch (MarketImpactAiResponseParseException exception)
        {
            return Ok(new ParsePreviewResponse
            {
                IsValid = false,
                ParsedResponse = null,
                Error = exception.Message
            });
        }
    }

    private static TruthPost CreatePreviewPost(string content)
    {
        return new TruthPost
        {
            Source = "preview",
            Author = "preview",
            ExternalId = Guid.NewGuid().ToString("N"),
            Url = "about:blank",
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow,
            CollectedAt = DateTimeOffset.UtcNow,
            SavedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
