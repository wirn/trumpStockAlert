using MailKit.Security;
using MimeKit;

namespace TrumpStockAlert.Api.Services;

public sealed class MailKitSmtpClientFactory : ISmtpClientFactory
{
    public ISmtpClient Create() => new MailKitSmtpClient();

    private sealed class MailKitSmtpClient : ISmtpClient
    {
        private readonly MailKit.Net.Smtp.SmtpClient client = new();

        public async Task SendAsync(
            SmtpClientSettings settings,
            MimeMessage message,
            CancellationToken cancellationToken)
        {
            var socketOptions = settings.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(
                settings.Host,
                settings.Port,
                socketOptions,
                cancellationToken);
            await client.AuthenticateAsync(
                settings.Username,
                settings.Password,
                cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }

        public void Dispose() => client.Dispose();
    }
}
