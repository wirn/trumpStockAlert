using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TrumpStockAlert.Api.Models;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Tests;

public sealed class OpenAiMarketImpactAnalyzerTests
{
    private static TruthPost MakePost(string content = "Tariffs on China may affect markets.") => new()
    {
        Id = 1,
        Source = "truth_social",
        Author = "realDonaldTrump",
        ExternalId = "123",
        Url = "https://example.com/post/123",
        Content = content,
        CreatedAt = DateTimeOffset.UtcNow,
        CollectedAt = DateTimeOffset.UtcNow,
        SavedAtUtc = DateTimeOffset.UtcNow
    };

    private static OpenAiMarketImpactAnalyzer CreateAnalyzer(
        string? apiKey = "test-key",
        string? model = "test-model",
        string responseJson = """
            {
              "marketImpactScore": 82,
              "confidenceScore": 77,
              "direction": "negative",
              "reasoning": "Mentions tariffs and China.",
              "affectedAssets": ["SP500", "USD", "China equities"]
            }
            """)
    {
        var configValues = new Dictionary<string, string?>();
        if (apiKey is not null)
        {
            configValues["OpenAI:ApiKey"] = apiKey;
        }
        if (model is not null)
        {
            configValues["OpenAI:Model"] = model;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        return new OpenAiMarketImpactAnalyzer(
            configuration,
            new MarketImpactPromptBuilder(),
            new MarketImpactAiResponseParser(),
            new FakeOpenAiChatCompletionClient(responseJson),
            NullLogger<OpenAiMarketImpactAnalyzer>.Instance);
    }

    [Fact]
    public async Task AnalyzeAsync_MissingApiKey_ThrowsClearError()
    {
        var analyzer = CreateAnalyzer(apiKey: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => analyzer.AnalyzeAsync(MakePost()));

        Assert.Contains("OpenAI API key is missing", ex.Message);
    }

    [Fact]
    public async Task AnalyzeAsync_ValidResponse_ParsesContract()
    {
        var analyzer = CreateAnalyzer();

        var result = await analyzer.AnalyzeAsync(MakePost());

        Assert.Equal(82, result.MarketImpactScore);
        Assert.Equal(77, result.ConfidenceScore);
        Assert.Equal("negative", result.Direction);
        Assert.Contains("SP500", result.AffectedAssets);
        Assert.StartsWith("openai-test-model-v1", result.AnalyzerVersion);
        Assert.Contains("confidenceScore", result.RawAiResponse);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidJson_ThrowsInvalidOperationException()
    {
        var analyzer = CreateAnalyzer(responseJson: "not json");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => analyzer.AnalyzeAsync(MakePost()));

        Assert.Contains("invalid market impact JSON", ex.Message);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidContract_ThrowsInvalidOperationException()
    {
        var analyzer = CreateAnalyzer(responseJson: """
            {
              "marketImpactScore": 82,
              "confidenceScore": 77,
              "direction": "bullish",
              "reasoning": "Invalid direction.",
              "affectedAssets": ["SP500"]
            }
            """);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => analyzer.AnalyzeAsync(MakePost()));

        Assert.Contains("direction", ex.Message);
    }

    private sealed class FakeOpenAiChatCompletionClient(string responseJson) : IOpenAiChatCompletionClient
    {
        public Task<string> CompleteJsonAsync(
            string apiKey,
            string model,
            string prompt,
            int timeoutSeconds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(responseJson);
        }
    }
}
