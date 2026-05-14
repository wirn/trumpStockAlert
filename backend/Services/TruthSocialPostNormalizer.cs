using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using TrumpStockAlert.Api.DTOs;

namespace TrumpStockAlert.Api.Services;

public static partial class TruthSocialPostNormalizer
{
    public const string NoTextContent = "[No text content]";

    public static CreateTruthPostRequest Normalize(
        string username,
        JsonElement rawPost,
        DateTimeOffset collectedAt)
    {
        var author = username.Trim().TrimStart('@');
        var externalId = RequiredString(rawPost, "id");
        var createdAt = ParseCreatedAt(RequiredString(rawPost, "created_at"), externalId);
        var content = ResolveContent(rawPost);
        var url = OptionalString(rawPost, "url")
            ?? $"https://truthsocial.com/@{author}/posts/{externalId}";

        return new CreateTruthPostRequest
        {
            Source = "truthsocial",
            Author = author,
            ExternalId = externalId,
            Url = url,
            Content = content,
            CreatedAt = createdAt,
            CollectedAt = collectedAt,
            Raw = rawPost.Clone()
        };
    }

    public static string? TryGetExternalId(JsonElement rawPost)
    {
        return rawPost.ValueKind == JsonValueKind.Object
            ? OptionalString(rawPost, "id")
            : null;
    }

    public static bool IsFallbackContent(string content)
    {
        return string.Equals(content.Trim(), NoTextContent, StringComparison.Ordinal);
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        var value = OptionalString(element, propertyName);
        if (value is null)
        {
            throw new InvalidOperationException($"Truth Social post is missing required property '{propertyName}'.");
        }

        return value;
    }

    private static string? OptionalString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTimeOffset ParseCreatedAt(string value, string externalId)
    {
        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        throw new InvalidOperationException(
            $"Truth Social post {externalId} has invalid created_at value.");
    }

    private static string ResolveContent(JsonElement rawPost)
    {
        var candidates = new[]
        {
            CleanOptionalString(rawPost, "content"),
            CleanOptionalString(rawPost, "spoiler_text"),
            CleanOptionalString(rawPost, "text"),
            CleanOptionalString(rawPost, "title"),
            ContentFromCard(rawPost),
            ContentFromEmbeddedPost(rawPost, "quote"),
            ContentFromEmbeddedPost(rawPost, "reblog"),
            ContentFromMediaAttachments(rawPost)
        };

        return candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate))
            ?? NoTextContent;
    }

    private static string? CleanOptionalString(JsonElement element, string propertyName)
    {
        var value = OptionalString(element, propertyName);
        return value is null ? null : CleanContent(value);
    }

    private static string? ContentFromCard(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("card", out var card)
            || card.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return CleanOptionalString(card, "title")
            ?? CleanOptionalString(card, "description");
    }

    private static string? ContentFromEmbeddedPost(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var embedded)
            || embedded.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return CleanOptionalString(embedded, "content")
            ?? CleanOptionalString(embedded, "spoiler_text")
            ?? CleanOptionalString(embedded, "text")
            ?? CleanOptionalString(embedded, "title")
            ?? ContentFromCard(embedded)
            ?? ContentFromMediaAttachments(embedded);
    }

    private static string CleanContent(string value)
    {
        var decoded = WebUtility.HtmlDecode(value);
        var withoutTags = HtmlTagRegex().Replace(decoded, " ");
        var decodedWithoutTags = WebUtility.HtmlDecode(withoutTags);
        return WhitespaceRegex().Replace(decodedWithoutTags, " ").Trim();
    }

    private static string? ContentFromMediaAttachments(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("media_attachments", out var mediaAttachments)
            || mediaAttachments.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var descriptions = mediaAttachments
            .EnumerateArray()
            .SelectMany(ContentFromMediaAttachment)
            .Where(description => !string.IsNullOrWhiteSpace(description))
            .ToList();

        return descriptions.Count == 0
            ? null
            : WhitespaceRegex().Replace(string.Join(" ", descriptions), " ").Trim();
    }

    private static IEnumerable<string> ContentFromMediaAttachment(JsonElement attachment)
    {
        if (attachment.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        var directDescription = CleanOptionalString(attachment, "description")
            ?? CleanOptionalString(attachment, "title")
            ?? CleanOptionalString(attachment, "name");
        if (!string.IsNullOrWhiteSpace(directDescription))
        {
            yield return directDescription;
        }

        if (attachment.TryGetProperty("meta", out var meta)
            && meta.ValueKind == JsonValueKind.Object)
        {
            var metaDescription = ContentFromMediaMeta(meta);
            if (!string.IsNullOrWhiteSpace(metaDescription))
            {
                yield return metaDescription;
            }
        }
    }

    private static string? ContentFromMediaMeta(JsonElement meta)
    {
        var directDescription = CleanOptionalString(meta, "description")
            ?? CleanOptionalString(meta, "title")
            ?? CleanOptionalString(meta, "name");
        if (!string.IsNullOrWhiteSpace(directDescription))
        {
            return directDescription;
        }

        foreach (var nestedName in new[] { "original", "small", "focus" })
        {
            if (meta.TryGetProperty(nestedName, out var nested)
                && nested.ValueKind == JsonValueKind.Object)
            {
                var nestedDescription = ContentFromMediaMeta(nested);
                if (!string.IsNullOrWhiteSpace(nestedDescription))
                {
                    return nestedDescription;
                }
            }
        }

        return null;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
