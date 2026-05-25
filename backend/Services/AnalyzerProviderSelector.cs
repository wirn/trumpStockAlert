namespace TrumpStockAlert.Api.Services;

public static class AnalyzerProviderSelector
{
    public const string MockProvider = "Mock";
    public const string OpenAiProvider = "OpenAI";

    public static IMarketImpactAnalyzer Select(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var provider = configuration["Analyzer:Provider"];

        if (string.Equals(provider, OpenAiProvider, StringComparison.OrdinalIgnoreCase))
        {
            return serviceProvider.GetRequiredService<OpenAiMarketImpactAnalyzer>();
        }

        return serviceProvider.GetRequiredService<MockMarketImpactAnalyzer>();
    }
}
