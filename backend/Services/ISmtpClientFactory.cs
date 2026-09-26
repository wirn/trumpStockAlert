namespace TrumpStockAlert.Api.Services;

public interface ISmtpClientFactory
{
    ISmtpClient Create();
}
