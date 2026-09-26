using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Tests;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendAsync_UsesGmailDefaultsAndBuildsMultipartMessage()
    {
        var factory = new CapturingSmtpClientFactory();
        var sender = CreateSender(factory);

        await sender.SendAsync(new AlertEmailMessage
        {
            Recipient = "recipient@example.com",
            Subject = "Market alert",
            Body = "Alert body",
            HtmlBody = "<strong>Alert body</strong>"
        });

        Assert.NotNull(factory.Settings);
        Assert.Equal("smtp.gmail.com", factory.Settings.Host);
        Assert.Equal(587, factory.Settings.Port);
        Assert.True(factory.Settings.EnableSsl);
        Assert.Equal("sender@gmail.com", factory.Settings.Username);
        Assert.Equal("test-app-password", factory.Settings.Password);

        Assert.NotNull(factory.Client.Message);
        Assert.Equal("sender@gmail.com", factory.Client.Message.From);
        Assert.Equal("recipient@example.com", factory.Client.Message.Recipient);
        Assert.Equal("Market alert", factory.Client.Message.Subject);
        Assert.Equal("Alert body", factory.Client.Message.PlainTextBody);
        Assert.Equal("<strong>Alert body</strong>", factory.Client.Message.HtmlBody);
    }

    [Fact]
    public async Task SendAsync_UsesExplicitSmtpConfigurationAndFromAddress()
    {
        var factory = new CapturingSmtpClientFactory();
        var sender = CreateSender(
            factory,
            new Dictionary<string, string?>
            {
                ["SMTP_HOST"] = "smtp.example.com",
                ["SMTP_PORT"] = "2525",
                ["SMTP_USE_STARTTLS"] = "false",
                ["SMTP_USERNAME"] = "smtp-user",
                ["SMTP_PASSWORD"] = "smtp-password",
                ["EMAIL_FROM"] = "alerts@example.com"
            });

        await sender.SendAsync(new AlertEmailMessage
        {
            Recipient = "recipient@example.com",
            Subject = "Market alert",
            Body = "Alert body"
        });

        Assert.NotNull(factory.Settings);
        Assert.Equal("smtp.example.com", factory.Settings.Host);
        Assert.Equal(2525, factory.Settings.Port);
        Assert.False(factory.Settings.EnableSsl);
        Assert.Equal("alerts@example.com", factory.Client.Message?.From);
        Assert.Equal("Alert body", factory.Client.Message?.PlainTextBody);
        Assert.Null(factory.Client.Message?.HtmlBody);
    }

    [Fact]
    public async Task SendAsync_MissingPassword_ThrowsWithoutCreatingClient()
    {
        var factory = new CapturingSmtpClientFactory();
        var sender = CreateSender(
            factory,
            new Dictionary<string, string?>
            {
                ["SMTP_USERNAME"] = "sender@gmail.com",
                ["SMTP_PASSWORD"] = ""
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(new AlertEmailMessage
            {
                Recipient = "recipient@example.com",
                Subject = "Market alert",
                Body = "Alert body"
            }));

        Assert.Contains("SMTP_PASSWORD", exception.Message);
        Assert.Null(factory.Settings);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public async Task SendAsync_InvalidPort_ThrowsWithoutCreatingClient(string port)
    {
        var factory = new CapturingSmtpClientFactory();
        var sender = CreateSender(
            factory,
            new Dictionary<string, string?>
            {
                ["SMTP_USERNAME"] = "sender@gmail.com",
                ["SMTP_PASSWORD"] = "test-app-password",
                ["SMTP_PORT"] = port
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(new AlertEmailMessage
            {
                Recipient = "recipient@example.com",
                Subject = "Market alert",
                Body = "Alert body"
            }));

        Assert.Contains("SMTP_PORT", exception.Message);
        Assert.Null(factory.Settings);
    }

    private static SmtpEmailSender CreateSender(
        CapturingSmtpClientFactory factory,
        Dictionary<string, string?>? values = null)
    {
        values ??= new Dictionary<string, string?>
        {
            ["SMTP_USERNAME"] = "sender@gmail.com",
            ["SMTP_PASSWORD"] = "test-app-password"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new SmtpEmailSender(
            configuration,
            factory,
            NullLogger<SmtpEmailSender>.Instance);
    }

    private sealed class CapturingSmtpClientFactory : ISmtpClientFactory
    {
        public CapturingSmtpClientFactory()
        {
            Client = new CapturingSmtpClient(settings => Settings = settings);
        }

        public SmtpClientSettings? Settings { get; private set; }

        public CapturingSmtpClient Client { get; }

        public ISmtpClient Create()
        {
            return Client;
        }

    }

    private sealed class CapturingSmtpClient(Action<SmtpClientSettings> captureSettings) : ISmtpClient
    {
        public CapturedMessage? Message { get; private set; }

        public Task SendAsync(
            SmtpClientSettings settings,
            MimeMessage message,
            CancellationToken cancellationToken)
        {
            captureSettings(settings);

            Message = new CapturedMessage(
                message.From.Mailboxes.Single().Address,
                message.To.Mailboxes.Single().Address,
                message.Subject ?? string.Empty,
                message.TextBody,
                message.HtmlBody);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed record CapturedMessage(
        string? From,
        string Recipient,
        string Subject,
        string? PlainTextBody,
        string? HtmlBody);
}
