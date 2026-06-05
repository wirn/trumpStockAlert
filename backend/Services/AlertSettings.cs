namespace TrumpStockAlert.Api.Services;

public sealed class AlertSettings
{
    public const string SectionName = "Alerts";

    public bool Enabled { get; init; } = true;

    public int Threshold { get; init; } = 70;

    public string Recipient { get; init; } = "log-only@trumpstockalert.local";

    public string AlertType { get; init; } = "MarketImpact";
}
