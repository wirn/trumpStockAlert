using System.Net;

namespace TrumpStockAlert.Api.Services;

public sealed class TruthSocialCollectorClientException : Exception
{
    public TruthSocialCollectorClientException(
        string message,
        HttpStatusCode? statusCode,
        string requestPath)
        : base(message)
    {
        StatusCode = statusCode;
        RequestPath = requestPath;
    }

    public HttpStatusCode? StatusCode { get; }

    public string RequestPath { get; }
}
