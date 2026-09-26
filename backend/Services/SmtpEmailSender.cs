using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace TrumpStockAlert.Api.Services;

public sealed class SmtpEmailSender(
    IConfiguration configuration,
    ISmtpClientFactory smtpClientFactory,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private const string DefaultHost = "smtp.gmail.com";
    private const int DefaultPort = 587;

    public async Task SendAsync(
        AlertEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var settings = GetSettings(configuration);
        var fromEmail = GetOptionalConfigurationValue(
                configuration,
                "EMAIL_FROM",
                "Email:From",
                "Smtp:FromEmail")
            ?? settings.Username;

        using var mailMessage = BuildMessage(message, fromEmail);
        using var smtpClient = smtpClientFactory.Create();

        try
        {
            await smtpClient.SendAsync(settings, mailMessage, cancellationToken);
            logger.LogInformation(
                "SMTP alert email sent. Recipient: {Recipient}. Subject: {Subject}.",
                message.Recipient,
                message.Subject);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is AuthenticationException
            or SmtpCommandException
            or SmtpProtocolException
            or SslHandshakeException
            or IOException)
        {
            logger.LogError(
                "SMTP alert email failed. Recipient: {Recipient}. Subject: {Subject}. ErrorType: {ErrorType}.",
                message.Recipient,
                message.Subject,
                exception.GetType().Name);

            throw new InvalidOperationException("SMTP alert email failed.", exception);
        }
    }

    private static MimeMessage BuildMessage(AlertEmailMessage message, string fromEmail)
    {
        var mailMessage = new MimeMessage
        {
            Subject = message.Subject
        };
        mailMessage.From.Add(MailboxAddress.Parse(fromEmail));
        mailMessage.To.Add(MailboxAddress.Parse(message.Recipient));

        var bodyBuilder = new BodyBuilder
        {
            TextBody = message.Body,
            HtmlBody = string.IsNullOrWhiteSpace(message.HtmlBody)
                ? null
                : message.HtmlBody
        };
        mailMessage.Body = bodyBuilder.ToMessageBody();

        return mailMessage;
    }

    private static SmtpClientSettings GetSettings(IConfiguration configuration)
    {
        var host = GetOptionalConfigurationValue(configuration, "SMTP_HOST", "Smtp:Host")
            ?? DefaultHost;
        var username = GetRequiredConfigurationValue(
            configuration,
            "SMTP_USERNAME",
            "Smtp:Username");
        var password = GetRequiredConfigurationValue(
            configuration,
            "SMTP_PASSWORD",
            "Smtp:Password");

        var portValue = GetOptionalConfigurationValue(configuration, "SMTP_PORT", "Smtp:Port");
        var port = DefaultPort;
        if (portValue is not null
            && (!int.TryParse(portValue, out port) || port is < 1 or > 65535))
        {
            throw new InvalidOperationException(
                "SMTP_PORT must be an integer between 1 and 65535 when EMAIL_PROVIDER=Smtp.");
        }

        var enableSslValue = GetOptionalConfigurationValue(
            configuration,
            "SMTP_USE_STARTTLS",
            "SMTP_ENABLE_SSL",
            "Smtp:EnableSsl");
        var enableSsl = true;
        if (enableSslValue is not null && !bool.TryParse(enableSslValue, out enableSsl))
        {
            throw new InvalidOperationException(
                "SMTP_USE_STARTTLS must be true or false when EMAIL_PROVIDER=Smtp.");
        }

        return new SmtpClientSettings(host, port, enableSsl, username, password);
    }

    private static string GetRequiredConfigurationValue(
        IConfiguration configuration,
        params string[] keys) =>
        GetOptionalConfigurationValue(configuration, keys)
        ?? throw new InvalidOperationException(
            $"{keys[0]} is required when EMAIL_PROVIDER=Smtp.");

    private static string? GetOptionalConfigurationValue(
        IConfiguration configuration,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
