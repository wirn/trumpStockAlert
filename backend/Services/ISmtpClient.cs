using MimeKit;

namespace TrumpStockAlert.Api.Services;

public interface ISmtpClient : IDisposable
{
    Task SendAsync(
        SmtpClientSettings settings,
        MimeMessage message,
        CancellationToken cancellationToken);
}
