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

    public required bool HasImage { get; init; }

    public required IReadOnlyList<string> ImageUrls { get; init; }

    public PostAnalysisResponse? Analysis { get; init; }

    public static TruthPostResponse FromEntity(TruthPost post)
    {
        var imageUrls = ExtractImageUrls(post.RawJson);

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
            HasImage = imageUrls.Count > 0,
            ImageUrls = imageUrls,
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

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ExtractImageUrls(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (!TryGetProperty(document.RootElement, "media_attachments", out var mediaAttachments)
                || mediaAttachments.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return mediaAttachments
                .EnumerateArray()
                .Where(IsImageAttachment)
                .SelectMany(GetAttachmentUrls)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool IsImageAttachment(JsonElement attachment)
    {
        if (attachment.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetProperty(attachment, "type", out var type)
            || type.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return string.Equals(type.GetString(), "image", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetAttachmentUrls(JsonElement attachment)
    {
        foreach (var propertyName in new[] { "url", "preview_url", "remote_url" })
        {
            if (TryGetProperty(attachment, propertyName, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                var url = value.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    yield return url;
                }
            }
        }
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
