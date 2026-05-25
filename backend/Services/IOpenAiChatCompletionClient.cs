namespace TrumpStockAlert.Api.Services;

public interface IOpenAiChatCompletionClient
{
    Task<string> CompleteJsonAsync(
        string apiKey,
        string model,
        string prompt,
        int timeoutSeconds,
        CancellationToken cancellationToken = default);
}
