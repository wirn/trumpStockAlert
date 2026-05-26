using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public sealed class OpenAiMarketImpactAnalyzer(
    IConfiguration configuration,
    MarketImpactPromptBuilder promptBuilder,
    MarketImpactAiResponseParser responseParser,
    IOpenAiChatCompletionClient chatClient,
    ILogger<OpenAiMarketImpactAnalyzer> logger) : IMarketImpactAnalyzer
{
    private const int DefaultTimeoutSeconds = 30;
    private const string DefaultModel = "gpt-5.1-mini";

    public async Task<MarketImpactAnalysisResult> AnalyzeAsync(
        TruthPost post,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is missing. Configure OpenAI:ApiKey or OpenAI__ApiKey when Analyzer:Provider is OpenAI.");
        }

        var model = configuration["OpenAI:Model"];
        if (string.IsNullOrWhiteSpace(model))
        {
            model = DefaultModel;
        }

        var timeoutSeconds = configuration.GetValue("OpenAI:TimeoutSeconds", DefaultTimeoutSeconds);
        if (timeoutSeconds < 1)
        {
            throw new InvalidOperationException("OpenAI timeout must be at least 1 second.");
        }

        try
        {
            logger.LogInformation(
                "Running OpenAI market impact analysis for post {PostId} ({ExternalId}) using model {Model}.",
                post.Id,
                post.ExternalId,
                model);

            var prompt = promptBuilder.BuildPrompt(post);
            var rawJson = await chatClient.CompleteJsonAsync(
                apiKey,
                model,
                prompt,
                timeoutSeconds,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                throw new InvalidOperationException("OpenAI returned an empty analysis response.");
            }

            var normalizedRawJson = responseParser.NormalizeAndValidate(rawJson);
            var parsed = responseParser.ParseAndValidate(normalizedRawJson);
            return new MarketImpactAnalysisResult
            {
                MarketImpactScore = parsed.MarketImpactScore,
                ConfidenceScore = parsed.ConfidenceScore,
                Direction = parsed.Direction,
                Reasoning = parsed.Reasoning,
                AffectedAssets = parsed.AffectedAssets,
                AnalyzerVersion = $"openai-{model}-v1",
                RawAiResponse = normalizedRawJson
            };
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "OpenAI market impact analysis was cancelled for post {PostId} ({ExternalId}).",
                post.Id,
                post.ExternalId);
            throw;
        }
        catch (MarketImpactAiResponseParseException exception)
        {
            logger.LogError(
                exception,
                "OpenAI returned invalid market impact JSON for post {PostId} ({ExternalId}).",
                post.Id,
                post.ExternalId);
            throw new InvalidOperationException(
                $"OpenAI returned invalid market impact JSON: {exception.Message}",
                exception);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "OpenAI market impact analysis failed for post {PostId} ({ExternalId}).",
                post.Id,
                post.ExternalId);
            throw;
        }
    }

}
