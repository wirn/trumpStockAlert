namespace TrumpStockAlert.Api.Services;

public interface IEmailSender
{
    Task SendAsync(AlertEmailMessage message, CancellationToken cancellationToken = default);
}
