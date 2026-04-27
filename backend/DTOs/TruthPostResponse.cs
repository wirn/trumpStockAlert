using System.Text.Json;
using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.DTOs;

public sealed class TruthPostResponse
{
    public required int Id { get; init; }

    public required string Source { get; init; }

    public required string Author { get; init; }

    public required string ExternalId { get; init; }

    public required string Url { get; init; }

    public required string Content { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset CollectedAt { get; init; }

    public required DateTimeOffset SavedAtUtc { get; init; }

    public JsonElement? Raw { get; init; }

    public PostAnalysisResponse? Analysis { get; init; }

    public static TruthPostResponse FromEntity(TruthPost post)
    {
        return new TruthPostResponse
        {
            Id = post.Id,
            Source = post.Source,
            Author = post.Author,
            ExternalId = post.ExternalId,
            Url = post.Url,
            Content = post.Content,
            CreatedAt = post.CreatedAt,
            CollectedAt = post.CollectedAt,
            SavedAtUtc = post.SavedAtUtc,
            Raw = ParseRaw(post.RawJson),
            Analysis = post.Analysis is null
                ? null
                : PostAnalysisResponse.FromEntity(post.Analysis)
        };
    }

    private static JsonElement? ParseRaw(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(rawJson);
        return document.RootElement.Clone();
    }
}
