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
    /// <summary>
    /// Runs a mock analysis for preview purposes.
    /// </summary>
    /// <remarks>
    /// Does not call OpenAI and does not persist any data.
    /// Useful for testing the analysis logic locally.
    /// </remarks>
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

    /// <summary>
    /// Runs an OpenAI-powered analysis preview.
    /// </summary>
    /// <remarks>
    /// Sends the provided content to OpenAI and returns a structured analysis.
    /// Does not save anything to the database.
    /// Useful for prompt tuning and AI validation.
    /// </remarks>
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

    /// <summary>
    /// Runs market-impact analysis for unprocessed posts.
    /// </summary>
    /// <remarks>
    /// Finds posts without an analysis, analyzes them using the configured analyzer, and saves the results in the database.
    /// Safe to run multiple times; already analyzed posts are skipped.
    /// </remarks>
    [HttpPost("run")]
    [ProducesResponseType(typeof(PostAnalysisRunResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PostAnalysisRunResult>> RunAnalysis(
        CancellationToken cancellationToken)
    {
        var result = await analysisRunner.AnalyzePendingPostsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Generates the prompt sent to the AI analyzer.
    /// </summary>
    /// <remarks>
    /// Useful for debugging and verifying how input content is transformed into an AI prompt.
    /// Does not call OpenAI or persist data.
    /// </remarks>
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

    /// <summary>
    /// Parses a raw AI response into a structured format.
    /// </summary>
    /// <remarks>
    /// Used to validate and debug parsing logic for AI responses.
    /// Does not call OpenAI or persist data.
    /// </remarks>
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
