namespace TrumpStockAlert.Api.Services;

public sealed class AlertEmailMessage
{
    public required string Recipient { get; init; }

    public required string Subject { get; init; }

    public required string Body { get; init; }

    public string? HtmlBody { get; init; }
}
