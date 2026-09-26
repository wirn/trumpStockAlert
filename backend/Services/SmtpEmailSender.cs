using System.Net.Mail;
using System.Net.Mime;
using System.Text;

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
        using var smtpClient = smtpClientFactory.Create(settings);

        try
        {
            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
            logger.LogInformation(
                "SMTP alert email sent. Recipient: {Recipient}. Subject: {Subject}.",
                message.Recipient,
                message.Subject);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SmtpException exception)
        {
            logger.LogError(
                "SMTP alert email failed. Recipient: {Recipient}. Subject: {Subject}. StatusCode: {StatusCode}.",
                message.Recipient,
                message.Subject,
                exception.StatusCode);

            throw new InvalidOperationException("SMTP alert email failed.", exception);
        }
    }

    private static MailMessage BuildMessage(AlertEmailMessage message, string fromEmail)
    {
        var mailMessage = new MailMessage
        {
            From = new MailAddress(fromEmail),
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        mailMessage.To.Add(new MailAddress(message.Recipient));

        if (string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            mailMessage.Body = message.Body;
            mailMessage.IsBodyHtml = false;
            return mailMessage;
        }

        mailMessage.AlternateViews.Add(
            AlternateView.CreateAlternateViewFromString(
                message.Body,
                Encoding.UTF8,
                MediaTypeNames.Text.Plain));
        mailMessage.AlternateViews.Add(
            AlternateView.CreateAlternateViewFromString(
                message.HtmlBody,
                Encoding.UTF8,
                MediaTypeNames.Text.Html));

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
