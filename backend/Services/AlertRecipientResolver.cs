namespace TrumpStockAlert.Api.Services;

public sealed class AlertRecipientResolver(IConfiguration configuration)
{
    private const string EmailToConfigName = "EMAIL_TO";
    private const string DefaultRecipient = "log-only@trumpstockalert.local";

    public IReadOnlyList<string> Resolve(AlertSettings settings)
    {
        var configuredRecipients = configuration[EmailToConfigName];
        if (string.IsNullOrWhiteSpace(configuredRecipients))
        {
            configuredRecipients = settings.Recipient;
        }

        var recipients = configuredRecipients
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(recipient => !string.IsNullOrWhiteSpace(recipient))
            .ToArray();

        return recipients.Length == 0 ? [DefaultRecipient] : recipients;
    }
}
