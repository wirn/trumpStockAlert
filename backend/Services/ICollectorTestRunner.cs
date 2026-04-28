namespace TrumpStockAlert.Api.Services;

public interface ICollectorTestRunner
{
    Task<CollectorTestRunResult> RunTestAsync(CancellationToken cancellationToken);
}
