using System.Net;
using System.Net.Mail;

namespace TrumpStockAlert.Api.Services;

public sealed class SystemNetSmtpClientFactory : ISmtpClientFactory
{
    public ISmtpClient Create(SmtpClientSettings settings)
    {
        var client = new SmtpClient(settings.Host, settings.Port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = settings.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(settings.Username, settings.Password)
        };

        return new SystemNetSmtpClient(client);
    }

    private sealed class SystemNetSmtpClient(SmtpClient client) : ISmtpClient
    {
        public Task SendMailAsync(MailMessage message, CancellationToken cancellationToken) =>
            client.SendMailAsync(message, cancellationToken);

        public void Dispose() => client.Dispose();
    }
}
