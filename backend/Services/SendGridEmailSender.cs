using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TrumpStockAlert.Api.Services;

public sealed class SendGridEmailSender(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<SendGridEmailSender> logger) : IEmailSender
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SendAsync(
        AlertEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var apiKey = GetRequiredConfigurationValue(
            configuration,
            "SENDGRID_API_KEY",
            "SendGrid:ApiKey");
        var fromEmail = GetRequiredConfigurationValue(
            configuration,
            "EMAIL_FROM",
            "Email:From",
            "SendGrid:FromEmail");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v3/mail/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(BuildPayload(message, fromEmail), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation(
                "SendGrid alert email sent. Recipient: {Recipient}. Subject: {Subject}. StatusCode: {StatusCode}.",
                message.Recipient,
                message.Subject,
                (int)response.StatusCode);
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError(
            "SendGrid alert email failed. Recipient: {Recipient}. Subject: {Subject}. StatusCode: {StatusCode}. ResponseBody: {ResponseBody}",
            message.Recipient,
            message.Subject,
            (int)response.StatusCode,
            responseBody);

        throw new InvalidOperationException(
            $"SendGrid alert email failed with HTTP status {(int)response.StatusCode}.");
    }

    private static object BuildPayload(AlertEmailMessage message, string fromEmail)
    {
        return new
        {
            personalizations = new[]
            {
                new
                {
                    to = new[]
                    {
                        new { email = message.Recipient }
                    }
                }
            },
            from = new { email = fromEmail },
            subject = message.Subject,
            content = new[]
            {
                new
                {
                    type = "text/plain",
                    value = message.Body
                }
            }
        };
    }

    private static string GetRequiredConfigurationValue(
        IConfiguration configuration,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"{keys[0]} is required when EMAIL_PROVIDER=SendGrid.");
    }
}
