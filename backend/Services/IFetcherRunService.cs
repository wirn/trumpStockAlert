namespace TrumpStockAlert.Api.Services;

public interface IFetcherRunService
{
    Task LogRunAsync(
        string triggerType,
        CollectorRunResult result,
        CancellationToken cancellationToken);

    Task LogFailureAsync(
        string triggerType,
        DateTimeOffset startedAt,
        string message,
        CancellationToken cancellationToken);
}
