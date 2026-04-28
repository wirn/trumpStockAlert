namespace TrumpStockAlert.Api.Services;

public interface ICollectorProcessRunner
{
    Task<CollectorProcessRunResult> RunAsync(
        bool testMode,
        CancellationToken cancellationToken);
}
