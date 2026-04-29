using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Timer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public sealed class CollectorTimerFunction(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<CollectorTimerFunction> logger)
{
    private const string ApiKeyHeaderName = "x-api-key";
    private const int ResponseBodyLogMaxLength = 12000;

    [Function(nameof(CollectorTimerFunction))]
    public async Task RunAsync(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Collector timer trigger started at {Timestamp}.", DateTimeOffset.UtcNow);

        var backendBaseUrl = configuration["BackendBaseUrl"]?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(backendBaseUrl))
        {
            throw new InvalidOperationException("BackendBaseUrl is not configured.");
        }

        var collectorApiKey = configuration["Collector:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(collectorApiKey))
        {
            throw new InvalidOperationException("Collector__ApiKey is not configured.");
        }

        var requestUri = $"{backendBaseUrl}/api/collector/run";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };
        request.Headers.Add(ApiKeyHeaderName, collectorApiKey);

        logger.LogInformation("Calling collector backend endpoint {RequestUri}.", requestUri);

        using var response = await httpClientFactory
            .CreateClient(nameof(CollectorTimerFunction))
            .SendAsync(request, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var truncatedResponseBody = Truncate(responseBody, ResponseBodyLogMaxLength);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation(
                "Collector backend endpoint completed. StatusCode: {StatusCode}. ResponseBody: {ResponseBody}",
                (int)response.StatusCode,
                truncatedResponseBody);
            return;
        }

        logger.LogError(
            "Collector backend endpoint failed. StatusCode: {StatusCode}. ResponseBody: {ResponseBody}",
            (int)response.StatusCode,
            truncatedResponseBody);

        throw new HttpRequestException(
            $"Collector backend endpoint returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");
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
