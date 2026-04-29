using System.Net.Http.Headers;
using System.Text.Json;

namespace TrumpStockAlert.Api.Services;

public sealed class TruthSocialCollectorClient(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<TruthSocialCollectorClient> logger) : ITruthSocialCollectorClient
{
    private const string DefaultBaseUrl = "https://truthsocial.com";
    private const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36";
    private const string DefaultAcceptLanguage = "en-US,en;q=0.9";

    public async Task<IReadOnlyList<JsonElement>> FetchLatestPostsAsync(
        string username,
        int maxPosts,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = username.Trim().TrimStart('@');
        var accountId = configuration["Collector:TruthSocialAccountId"]?.Trim();
        if (string.IsNullOrWhiteSpace(accountId))
        {
            accountId = await LookupAccountIdAsync(normalizedUsername, cancellationToken);
        }

        var requestPath = $"/api/v1/accounts/{Uri.EscapeDataString(accountId)}/statuses";
        var requestUri = $"{requestPath}?exclude_replies=true&limit={maxPosts}";
        using var response = await SendGetAsync(requestUri, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        logger.LogInformation(
            "Truth Social request completed. Path: {RequestPath}. StatusCode: {StatusCode}.",
            requestPath,
            (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Truth Social statuses request failed. Path: {RequestPath}. StatusCode: {StatusCode}. ResponseBody: {ResponseBody}",
                requestPath,
                (int)response.StatusCode,
                Truncate(body, 2000));
            throw CreateRequestException("statuses request", response.StatusCode, requestPath);
        }

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Truth Social statuses response was not a JSON array.");
        }

        return document.RootElement
            .EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.Object)
            .Take(maxPosts)
            .Select(element => element.Clone())
            .ToList();
    }

    private async Task<string> LookupAccountIdAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var requestPath = "/api/v1/accounts/lookup";
        var requestUri = $"{requestPath}?acct={Uri.EscapeDataString(username)}";
        using var response = await SendGetAsync(requestUri, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        logger.LogInformation(
            "Truth Social request completed. Path: {RequestPath}. StatusCode: {StatusCode}.",
            requestPath,
            (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Truth Social account lookup failed. Path: {RequestPath}. StatusCode: {StatusCode}. ResponseBody: {ResponseBody}",
                requestPath,
                (int)response.StatusCode,
                Truncate(body, 2000));
            throw CreateRequestException("account lookup", response.StatusCode, requestPath);
        }

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("id", out var idElement)
            || idElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(idElement.GetString()))
        {
            throw new InvalidOperationException("Truth Social account lookup response did not include an id.");
        }

        return idElement.GetString()!;
    }

    private async Task<HttpResponseMessage> SendGetAsync(
        string requestUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Referrer = httpClient.BaseAddress;
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        request.Headers.Pragma.ParseAdd("no-cache");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

        return await httpClient.SendAsync(request, cancellationToken);
    }

    public static void ConfigureHttpClient(
        IServiceProvider serviceProvider,
        HttpClient client)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var baseUrl = configuration["Collector:TruthSocialBaseUrl"]?.Trim();
        client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl);
        client.Timeout = TimeSpan.FromSeconds(configuration.GetValue("Collector:TruthSocialTimeoutSeconds", 60));
        client.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain", 0.9));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(DefaultAcceptLanguage);
        client.DefaultRequestHeaders.ConnectionClose = false;
        client.DefaultRequestHeaders.TryAddWithoutValidation("DNT", "1");
    }

    private static TruthSocialCollectorClientException CreateRequestException(
        string operationName,
        System.Net.HttpStatusCode statusCode,
        string requestPath)
    {
        var message = statusCode == System.Net.HttpStatusCode.Forbidden
            ? $"Truth Social blocked the {operationName} with HTTP 403 (Forbidden). Path: {requestPath}."
            : $"Truth Social {operationName} failed with HTTP {(int)statusCode} ({statusCode}). Path: {requestPath}.";

        return new TruthSocialCollectorClientException(message, statusCode, requestPath);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }
}
