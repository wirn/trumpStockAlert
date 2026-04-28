using System.Net.Http.Headers;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Timer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public sealed class CollectorTimerFunction(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<CollectorTimerFunction> logger)
{
    private const string ApiKeyHeaderName = "x-api-key";

    [Function(nameof(CollectorTimerFunction))]
    public async Task RunAsync(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Collector timer trigger started at {Timestamp}.", DateTimeOffset.UtcNow);

        var backendBaseUrl = configuration["BackendBaseUrl"]?.Trim().TrimEnd('/');
        var collectorApiKey = configuration["CollectorApiKey"];

        if (string.IsNullOrWhiteSpace(backendBaseUrl))
        {
            logger.LogError("BackendBaseUrl is not configured.");
            return;
        }

        if (string.IsNullOrWhiteSpace(collectorApiKey))
        {
            logger.LogError("CollectorApiKey is not configured.");
            return;
        }

        var requestUri = $"{backendBaseUrl}/api/collector/run";
        logger.LogInformation("Calling collector backend endpoint {RequestUri}.", requestUri);

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add(ApiKeyHeaderName, collectorApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            logger.LogInformation(
                "Collector backend returned HTTP {StatusCode}. Response: {ResponseBody}",
                (int)response.StatusCode,
                Truncate(responseBody, 2000));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Collector timer trigger failed while calling backend.");
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }
}
