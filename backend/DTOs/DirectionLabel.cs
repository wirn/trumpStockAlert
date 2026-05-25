namespace TrumpStockAlert.Api.DTOs;

internal static class DirectionLabel
{
    public static string Normalize(string direction)
    {
        return direction.Trim().ToLowerInvariant() switch
        {
            "positive" => "positive",
            "negative" => "negative",
            "mixed" => "mixed",
            "neutral" => "neutral",
            _ => "neutral"
        };
    }
}
