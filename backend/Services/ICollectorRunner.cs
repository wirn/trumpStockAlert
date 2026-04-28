namespace TrumpStockAlert.Api.Services;

public interface ICollectorRunner
{
    Task<CollectorRunResult> RunAsync(CancellationToken cancellationToken);
}
