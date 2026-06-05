namespace TrumpStockAlert.Api.Services;

public interface IAlertEvaluator
{
    Task<AlertRunResult> RunAsync(CancellationToken cancellationToken = default);
}
