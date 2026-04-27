using System.Text.Json;

namespace TrumpStockAlert.Api.Services;

public static class JsonResponseText
{
    private static readonly JsonSerializerOptions PrettyPrintOptions = new()
    {
        WriteIndented = true
    };

    public static string NormalizeObjectJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new JsonException("JSON text is empty.");
        }

        var candidate = rawJson.Trim();
        using var document = JsonDocument.Parse(candidate);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.String)
        {
            var unwrapped = root.GetString();
            if (string.IsNullOrWhiteSpace(unwrapped))
            {
                throw new JsonException("JSON text is a string, but the string value is empty.");
            }

            candidate = unwrapped.Trim();
            using var unwrappedDocument = JsonDocument.Parse(candidate);
            return SerializeNormalized(unwrappedDocument.RootElement);
        }

        return SerializeNormalized(root);
    }

    private static string SerializeNormalized(JsonElement element)
    {
        if (element.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array)
        {
            throw new JsonException("JSON text must be an object or array.");
        }

        return JsonSerializer.Serialize(element, PrettyPrintOptions);
    }
}
