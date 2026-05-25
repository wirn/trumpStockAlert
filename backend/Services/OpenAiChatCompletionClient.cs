using System.ClientModel;
using OpenAI.Chat;

namespace TrumpStockAlert.Api.Services;

public sealed class OpenAiChatCompletionClient(
    ILogger<OpenAiChatCompletionClient> logger) : IOpenAiChatCompletionClient
{
    private const int MaxAttempts = 3;

    public async Task<string> CompleteJsonAsync(
        string apiKey,
        string model,
        string prompt,
        int timeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        var client = new ChatClient(model, apiKey);
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            MaxOutputTokenCount = 600
        };

        var completion = await CompleteChatWithRetryAsync(
            client,
            prompt,
            options,
            timeoutSeconds,
            cancellationToken);

        return completion.Content.Count > 0
            ? completion.Content[0].Text
            : string.Empty;
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
