using System.Net.Mail;

namespace TrumpStockAlert.Api.Services;

public interface ISmtpClient : IDisposable
{
    Task SendMailAsync(MailMessage message, CancellationToken cancellationToken);
}
