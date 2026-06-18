using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TrumpStockAlert.Api.Services;

namespace TrumpStockAlert.Api.Tests;

public sealed class SendGridEmailSenderTests
{
    [Fact]
    public async Task SendAsync_UsesSendGridWebApiPayloadAndAuthorizationHeader()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.Accepted));
        var sender = CreateSender(handler);

        await sender.SendAsync(new AlertEmailMessage
        {
            Recipient = "recipient@example.com",
            Subject = "Market alert",
            Body = "Alert body",
            HtmlBody = "<strong>Alert body</strong>"
        });

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal("/v3/mail/send", handler.Request.RequestUri?.PathAndQuery);
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal("test-sendgrid-key", handler.Request.Headers.Authorization?.Parameter);

        Assert.NotNull(handler.RequestBody);
        var body = handler.RequestBody;
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal("sender@example.com", root.GetProperty("from").GetProperty("email").GetString());
        Assert.Equal("Market alert", root.GetProperty("subject").GetString());
        Assert.Equal(
            "recipient@example.com",
            root.GetProperty("personalizations")[0].GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal(
            "text/plain",
            root.GetProperty("content")[0].GetProperty("type").GetString());
        Assert.Equal(
            "Alert body",
            root.GetProperty("content")[0].GetProperty("value").GetString());
        Assert.Equal(
            "text/html",
            root.GetProperty("content")[1].GetProperty("type").GetString());
        Assert.Equal(
            "<strong>Alert body</strong>",
            root.GetProperty("content")[1].GetProperty("value").GetString());
    }

    [Fact]
    public async Task SendAsync_MissingApiKey_ThrowsWithoutSendingRequest()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.Accepted));
        var sender = CreateSender(handler, apiKey: "");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(new AlertEmailMessage
            {
                Recipient = "recipient@example.com",
                Subject = "Market alert",
                Body = "Alert body"
            }));

        Assert.Contains("SENDGRID_API_KEY", exception.Message);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task SendAsync_SendGridFailure_ThrowsWithoutIncludingApiKey()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"errors":[{"message":"bad request"}]}""")
        });
        var sender = CreateSender(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(new AlertEmailMessage
            {
                Recipient = "recipient@example.com",
                Subject = "Market alert",
                Body = "Alert body"
            }));

        Assert.Contains("HTTP status 400", exception.Message);
        Assert.DoesNotContain("test-sendgrid-key", exception.Message);
    }

    private static SendGridEmailSender CreateSender(
        CapturingHandler handler,
        string apiKey = "test-sendgrid-key",
        string fromEmail = "sender@example.com")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SENDGRID_API_KEY"] = apiKey,
                ["EMAIL_FROM"] = fromEmail
            })
            .Build();

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.sendgrid.com")
        };

        return new SendGridEmailSender(
            httpClient,
            configuration,
            NullLogger<SendGridEmailSender>.Instance);
    }

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return response;
        }
    }
}
