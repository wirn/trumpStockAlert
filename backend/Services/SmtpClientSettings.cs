namespace TrumpStockAlert.Api.Services;

public sealed record SmtpClientSettings(
    string Host,
    int Port,
    bool EnableSsl,
    string Username,
    string Password);
