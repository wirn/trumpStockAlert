namespace TrumpStockAlert.Api.Services;

public sealed class LogOnlyEmailSender(ILogger<LogOnlyEmailSender> logger) : IEmailSender
{
    public Task SendAsync(AlertEmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Log-only alert email. Recipient: {Recipient}. Subject: {Subject}. Body: {Body}",
            message.Recipient,
            message.Subject,
            message.Body);

        return Task.CompletedTask;
    }
}
