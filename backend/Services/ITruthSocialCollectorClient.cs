using System.Text.Json;

namespace TrumpStockAlert.Api.Services;

public interface ITruthSocialCollectorClient
{
    Task<IReadOnlyList<JsonElement>> FetchLatestPostsAsync(
        string username,
        int maxPosts,
        CancellationToken cancellationToken);
}
