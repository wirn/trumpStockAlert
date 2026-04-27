using System.ClientModel;
using OpenAI.Chat;
using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public sealed class OpenAiMarketImpactAnalyzer(
    IConfiguration configuration,
    MarketImpactPromptBuilder promptBuilder,
    MarketImpactAiResponseParser responseParser,
    ILogger<OpenAiMarketImpactAnalyzer> logger) : IMarketImpactAnalyzer
{
    private const int DefaultTimeoutSeconds = 30;
    private const int MaxAttempts = 3;

    public async Task<MarketImpactAnalysisResult> AnalyzeAsync(
        TruthPost post,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is missing. Configure it with user-secrets key 'OpenAI:ApiKey' or environment variable 'OpenAI__ApiKey'.");
        }

        var model = configuration["OpenAI:Model"];
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException(
                "OpenAI model is missing. Configure 'OpenAI:Model', for example 'gpt-5.1-mini'.");
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

            var client = new ChatClient(model, apiKey);
            var prompt = promptBuilder.BuildPrompt(post);
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                MaxOutputTokenCount = 600
            };

            ChatCompletion completion = await CompleteChatWithRetryAsync(
                client,
                prompt,
                options,
                timeoutSeconds,
                cancellationToken);

            var rawJson = completion.Content.Count > 0
                ? completion.Content[0].Text
                : string.Empty;

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                throw new InvalidOperationException("OpenAI returned an empty analysis response.");
            }

            var normalizedRawJson = responseParser.NormalizeAndValidate(rawJson);
            var parsed = responseParser.ParseAndValidate(normalizedRawJson);
            return new MarketImpactAnalysisResult
            {
                MarketImpactScore = parsed.MarketImpactScore,
                Direction = parsed.Direction,
                Reasoning = parsed.Reasoning,
                AffectedAssets = parsed.AffectedAssets,
                Confidence = parsed.Confidence,
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
        catch (ClientResultException exception)
        {
            logger.LogError(
                exception,
                "OpenAI API request failed for post {PostId} ({ExternalId}). Status: {Status}.",
                post.Id,
                post.ExternalId,
                exception.Status);
            throw new InvalidOperationException(
                $"OpenAI API request failed with status {exception.Status}. See server logs for details.",
                exception);
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

    private async Task<ChatCompletion> CompleteChatWithRetryAsync(
        ChatClient client,
        string prompt,
        ChatCompletionOptions options,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                return await client.CompleteChatAsync(
                    [
                        new SystemChatMessage("You are a precise financial-market analysis assistant. Return only valid JSON."),
                        new UserChatMessage(prompt)
                    ],
                    options,
                    timeoutSource.Token);
            }
            catch (ClientResultException exception) when (ShouldRetry(exception.Status, attempt))
            {
                logger.LogWarning(
                    exception,
                    "Transient OpenAI API error on attempt {Attempt}/{MaxAttempts}. Status: {Status}. Retrying.",
                    attempt,
                    MaxAttempts,
                    exception.Status);

                await Task.Delay(GetRetryDelay(attempt), cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"OpenAI request timed out after {timeoutSeconds} seconds.");
            }
        }

        throw new InvalidOperationException("OpenAI request failed after retry attempts.");
    }

    private static bool ShouldRetry(int statusCode, int attempt)
    {
        return attempt < MaxAttempts
            && statusCode is 429 or 500 or 502 or 503 or 504;
    }

    private static TimeSpan GetRetryDelay(int attempt)
    {
        return TimeSpan.FromSeconds(attempt);
    }
}
