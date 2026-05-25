using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Tests;

public sealed class AnalyzerProviderSelectorTests
{
    [Fact]
    public void Select_MissingProvider_ReturnsMockAnalyzer()
    {
        using var provider = BuildProvider();

        var analyzer = AnalyzerProviderSelector.Select(provider);

        Assert.IsType<MockMarketImpactAnalyzer>(analyzer);
    }

    [Fact]
    public void Select_MockProvider_ReturnsMockAnalyzer()
    {
        using var provider = BuildProvider(("Analyzer:Provider", "Mock"));

        var analyzer = AnalyzerProviderSelector.Select(provider);

        Assert.IsType<MockMarketImpactAnalyzer>(analyzer);
    }

    [Fact]
    public void Select_OpenAiProvider_ReturnsOpenAiAnalyzer()
    {
        using var provider = BuildProvider(("Analyzer:Provider", "OpenAI"));

        var analyzer = AnalyzerProviderSelector.Select(provider);

        Assert.IsType<OpenAiMarketImpactAnalyzer>(analyzer);
    }

    private static ServiceProvider BuildProvider(params (string Key, string Value)[] values)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<MarketImpactPromptBuilder>();
        services.AddSingleton<MarketImpactAiResponseParser>();
        services.AddSingleton<IOpenAiChatCompletionClient, FakeOpenAiChatCompletionClient>();
        services.AddSingleton<ILogger<OpenAiMarketImpactAnalyzer>>(NullLogger<OpenAiMarketImpactAnalyzer>.Instance);
        services.AddSingleton<MockMarketImpactAnalyzer>();
        services.AddSingleton<OpenAiMarketImpactAnalyzer>();

        return services.BuildServiceProvider();
    }

    private sealed class FakeOpenAiChatCompletionClient : IOpenAiChatCompletionClient
    {
        public Task<string> CompleteJsonAsync(
            string apiKey,
            string model,
            string prompt,
            int timeoutSeconds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("{}");
        }
    }
}
