using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public interface IMarketImpactAnalyzer
{
    Task<MarketImpactAnalysisResult> AnalyzeAsync(
        TruthPost post,
        CancellationToken cancellationToken = default);
}
