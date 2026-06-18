using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public sealed class AlertEmailTemplateRenderer
{
    private static readonly HtmlEncoder Html = HtmlEncoder.Default;
    private const string AlertSubject = "Trump fibblar med marknaden!";

    public AlertEmailMessage Render(AlertSettings settings, PostAnalysis analysis)
    {
        var direction = GetDirectionDisplay(analysis.Direction);
        var plainTextBody = BuildPlainTextBody(settings, analysis, direction);
        var htmlBody = BuildHtmlBody(settings, analysis, direction);

        return new AlertEmailMessage
        {
            Recipient = settings.Recipient,
            Subject = AlertSubject,
            Body = plainTextBody,
            HtmlBody = htmlBody
        };
    }

    private static string BuildPlainTextBody(
        AlertSettings settings,
        PostAnalysis analysis,
        DirectionDisplay direction)
    {
        var lines = new List<string>
        {
            "TrumpStockAlert",
            "Real-time Truth Social Market Impact Analysis",
            string.Empty,
            $"Market Impact Score: {analysis.MarketImpactScore}/100",
            $"Direction: {direction.Label} ({FormatDirectionScale(analysis.Direction)})",
            "Direction range: -50 to +50",
            $"Confidence: {analysis.Confidence}/100",
            string.Empty,
            "AI reasoning:",
            ValueOrFallback(analysis.Reasoning, "No AI reasoning was provided."),
            string.Empty,
            "Original Truth Social post:",
            ValueOrFallback(analysis.Post.Content, "Original post text is unavailable."),
            string.Empty,
            $"Post created: {FormatTimestamp(analysis.Post.CreatedAt)}",
            $"Analyzed: {FormatTimestamp(analysis.AnalyzedAt)}"
        };

        if (!string.IsNullOrWhiteSpace(analysis.Post.Url))
        {
            lines.Add(string.Empty);
            lines.Add($"View source post: {analysis.Post.Url}");
        }

        var affectedAssets = ParseAffectedAssets(analysis.AffectedAssetsJson);
        if (affectedAssets.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add($"Affected assets: {string.Join(", ", affectedAssets)}");
        }

        lines.Add(string.Empty);
        lines.Add($"Alert type: {settings.AlertType}");
        lines.Add("Alert criteria: score > 60, confidence > 20, direction outside -4 to +4");
        lines.Add($"Analyzer version: {ValueOrFallback(analysis.AnalyzerVersion, "Unknown")}");
        lines.Add($"Post ID: {analysis.PostId}");
        lines.Add($"Analysis ID: {analysis.Id}");

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildHtmlBody(
        AlertSettings settings,
        PostAnalysis analysis,
        DirectionDisplay direction)
    {
        var scoreWidth = Math.Clamp(analysis.MarketImpactScore, 1, 100);
        var confidence = Math.Clamp(analysis.Confidence, 1, 100);
        var postAuthor = ValueOrFallback(analysis.Post.Author, "realDonaldTrump");
        var postUrl = GetAbsoluteUrlOrNull(analysis.Post.Url);
        var affectedAssets = ParseAffectedAssets(analysis.AffectedAssetsJson);

        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>TrumpStockAlert Market Impact Alert</title></head>");
        builder.AppendLine("<body style=\"margin:0;padding:0;background-color:#f3f4f6;font-family:Arial,Helvetica,sans-serif;color:#111827;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%;\">");
        builder.AppendLine("<center style=\"width:100%;background-color:#f3f4f6;\">");
        builder.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:collapse;background-color:#f3f4f6;\">");
        builder.AppendLine("<tr><td align=\"center\" style=\"padding:20px 12px;\">");
        builder.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:collapse;width:100%;max-width:600px;background-color:#ffffff;border:1px solid #d1d5db;\">");

        builder.AppendLine("<tr><td style=\"padding:20px 22px;border-bottom:1px solid #d1d5db;background-color:#ffffff;\">");
        builder.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:collapse;\"><tr>");
        builder.AppendLine("<td style=\"font-size:24px;line-height:30px;font-weight:700;color:#111827;vertical-align:middle;\">TrumpStockAlert</td>");
        builder.AppendLine("<td align=\"right\" style=\"font-size:14px;line-height:20px;color:#374151;vertical-align:middle;\">Real-time Truth Social<br>Market Impact Analysis</td>");
        builder.AppendLine("</tr></table>");
        builder.AppendLine("</td></tr>");

        builder.AppendLine("<tr><td style=\"padding:20px 18px 0 18px;\">");
        builder.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:separate;border-spacing:0;background-color:#f9fafb;border:1px solid #d1d5db;border-radius:8px;\">");
        builder.AppendLine("<tr><td style=\"padding:20px 22px;\">");
        builder.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:collapse;\"><tr>");
        builder.AppendLine("<td style=\"vertical-align:top;padding-right:12px;\">");
        builder.AppendLine("<div style=\"font-size:12px;line-height:16px;font-weight:700;color:#1d4ed8;text-transform:uppercase;letter-spacing:.05em;\">Market Impact Score</div>");
        builder.AppendLine($"<div style=\"font-size:48px;line-height:56px;font-weight:800;color:#111827;\">{analysis.MarketImpactScore}<span style=\"font-size:16px;line-height:20px;font-weight:400;color:#374151;\"> / 100</span></div>");
        builder.AppendLine("</td>");
        builder.AppendLine("<td align=\"right\" style=\"vertical-align:top;width:150px;\">");
        builder.AppendLine($"<div style=\"display:inline-block;background-color:{direction.BadgeBackground};color:{direction.BadgeColor};border:1px solid {direction.BorderColor};border-radius:999px;padding:8px 12px;font-size:14px;line-height:18px;font-weight:700;\">{Encode(direction.Label)} {Encode(FormatDirectionScale(analysis.Direction))}</div>");
        builder.AppendLine("<div style=\"padding-top:8px;font-size:12px;line-height:17px;color:#4b5563;\">Direction range: -50 to +50</div>");
        builder.AppendLine("</td>");
        builder.AppendLine("</tr></table>");
        builder.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:collapse;margin-top:14px;\"><tr><td style=\"height:8px;background-color:#e5e7eb;border-radius:999px;line-height:8px;font-size:0;\"><div style=\"height:8px;width:" + scoreWidth + "%;background-color:" + direction.AccentColor + ";border-radius:999px;line-height:8px;font-size:0;\">&nbsp;</div></td></tr></table>");
        builder.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:collapse;margin-top:8px;\"><tr>");
        builder.AppendLine("<td style=\"font-size:11px;line-height:16px;color:#4b5563;text-transform:uppercase;\">Bearish</td>");
        builder.AppendLine("<td align=\"center\" style=\"font-size:11px;line-height:16px;color:#4b5563;text-transform:uppercase;\">Neutral</td>");
        builder.AppendLine("<td align=\"right\" style=\"font-size:11px;line-height:16px;color:#4b5563;text-transform:uppercase;\">Bullish</td>");
        builder.AppendLine("</tr></table>");
        builder.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:collapse;margin-top:18px;border-top:1px solid #d1d5db;\"><tr>");
        builder.AppendLine("<td style=\"padding-top:14px;width:50%;font-size:11px;line-height:16px;color:#6b7280;text-transform:uppercase;\">Confidence<br><span style=\"font-size:22px;line-height:28px;font-weight:700;color:#111827;text-transform:none;\">" + confidence + " / 100</span></td>");
        builder.AppendLine("<td style=\"padding-top:14px;width:50%;font-size:11px;line-height:16px;color:#6b7280;text-transform:uppercase;border-left:1px solid #d1d5db;padding-left:18px;\">Analyzed<br><span style=\"font-size:14px;line-height:22px;font-weight:700;color:#111827;text-transform:none;\">" + Encode(FormatTimestamp(analysis.AnalyzedAt)) + "</span></td>");
        builder.AppendLine("</tr></table>");
        builder.AppendLine("</td></tr></table>");
        builder.AppendLine("</td></tr>");

        builder.AppendLine("<tr><td style=\"padding:18px 18px 0 18px;\">");
        builder.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:separate;border-spacing:0;background-color:#ffffff;border:1px solid #d1d5db;border-radius:8px;\">");
        builder.AppendLine("<tr><td style=\"padding:20px 22px;\">");
        builder.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:collapse;\"><tr>");
        builder.AppendLine("<td style=\"width:48px;vertical-align:top;\"><div style=\"width:40px;height:40px;border-radius:999px;background-color:#1d4ed8;color:#ffffff;text-align:center;font-size:18px;line-height:40px;font-weight:700;\">T</div></td>");
        builder.AppendLine("<td style=\"vertical-align:top;\">");
        builder.AppendLine("<div style=\"font-size:15px;line-height:20px;font-weight:700;color:#111827;\">Donald J. Trump <span style=\"color:#1d4ed8;\">&#10003;</span></div>");
        builder.AppendLine($"<div style=\"font-size:13px;line-height:18px;color:#6b7280;\">@{Encode(postAuthor)} &bull; {Encode(FormatTimestamp(analysis.Post.CreatedAt))}</div>");
        builder.AppendLine($"<div style=\"padding-top:12px;font-size:17px;line-height:26px;color:#111827;\">{EncodeMultiline(ValueOrFallback(analysis.Post.Content, "Original post text is unavailable."))}</div>");
        builder.AppendLine("</td></tr></table>");
        builder.AppendLine("</td></tr></table>");
        builder.AppendLine("</td></tr>");

        builder.AppendLine("<tr><td style=\"padding:18px 18px 0 18px;\">");
        builder.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:separate;border-spacing:0;background-color:#f9fafb;border:1px solid #d1d5db;border-radius:8px;\">");
        builder.AppendLine("<tr><td style=\"padding:20px 22px;\">");
        builder.AppendLine("<div style=\"font-size:20px;line-height:26px;font-weight:700;color:#111827;\">AI Impact Summary</div>");
        builder.AppendLine($"<div style=\"padding-top:12px;font-size:15px;line-height:24px;color:#111827;\"><strong>Reasoning:</strong> {EncodeMultiline(ValueOrFallback(analysis.Reasoning, "No AI reasoning was provided."))}</div>");
        if (affectedAssets.Count > 0)
        {
            builder.AppendLine("<div style=\"padding-top:14px;\">");
            foreach (var asset in affectedAssets)
            {
                builder.AppendLine($"<span style=\"display:inline-block;margin:0 6px 6px 0;padding:6px 10px;background-color:#eff6ff;border:1px solid #bfdbfe;border-radius:999px;color:#1d4ed8;font-size:12px;line-height:16px;font-weight:700;\">{Encode(asset)}</span>");
            }
            builder.AppendLine("</div>");
        }
        builder.AppendLine("</td></tr></table>");
        builder.AppendLine("</td></tr>");

        if (postUrl is not null)
        {
            builder.AppendLine("<tr><td align=\"center\" style=\"padding:26px 18px 4px 18px;\">");
            builder.AppendLine($"<a href=\"{Encode(postUrl)}\" style=\"display:inline-block;background-color:#1d4ed8;color:#ffffff;text-decoration:none;border-radius:999px;padding:14px 28px;font-size:16px;line-height:22px;font-weight:700;\">View Full Source</a>");
            builder.AppendLine("</td></tr>");
        }

        builder.AppendLine("<tr><td style=\"padding:26px 22px 22px 22px;background-color:#eef2ff;border-top:1px solid #d1d5db;\">");
        builder.AppendLine($"<div style=\"font-size:12px;line-height:20px;color:#4b5563;text-align:center;\">Alert type: {Encode(settings.AlertType)} &bull; Criteria: score &gt; 60, confidence &gt; 20, direction outside -4 to +4 &bull; Analyzer: {Encode(ValueOrFallback(analysis.AnalyzerVersion, "Unknown"))}</div>");
        builder.AppendLine($"<div style=\"font-size:12px;line-height:20px;color:#6b7280;text-align:center;\">Post ID {analysis.PostId} &bull; Analysis ID {analysis.Id}</div>");
        builder.AppendLine("<div style=\"padding-top:10px;font-size:13px;line-height:20px;color:#374151;text-align:center;\">&copy; TrumpStockAlert Financial Intelligence. Real-time Truth Social Impact Analysis.</div>");
        builder.AppendLine("</td></tr>");

        builder.AppendLine("</table>");
        builder.AppendLine("</td></tr></table>");
        builder.AppendLine("</center>");
        builder.AppendLine("</body></html>");

        return builder.ToString();
    }

    private static DirectionDisplay GetDirectionDisplay(int direction)
    {
        if (direction < 0)
        {
            return new DirectionDisplay(
                "Bearish",
                "Bearish Signal",
                "#dc2626",
                "#fee2e2",
                "#991b1b",
                "#fecaca");
        }

        if (direction > 0)
        {
            return new DirectionDisplay(
                "Bullish",
                "Bullish Signal",
                "#16a34a",
                "#dcfce7",
                "#166534",
                "#bbf7d0");
        }

        return new DirectionDisplay(
            "Neutral",
            "Neutral Signal",
            "#2563eb",
            "#eff6ff",
            "#1d4ed8",
            "#bfdbfe");
    }

    private static IReadOnlyList<string> ParseAffectedAssets(string? affectedAssetsJson)
    {
        if (string.IsNullOrWhiteSpace(affectedAssetsJson))
        {
            return [];
        }

        try
        {
            var assets = JsonSerializer.Deserialize<List<string>>(affectedAssetsJson);
            return assets?
                .Where(asset => !string.IsNullOrWhiteSpace(asset))
                .Select(asset => asset.Trim())
                .ToArray() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? GetAbsoluteUrlOrNull(string? value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.ToString();
        }

        return null;
    }

    private static string FormatTimestamp(DateTimeOffset value) =>
        value == default ? "Unavailable" : value.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'");

    private static string FormatSigned(int value) =>
        value > 0 ? $"+{value}" : value.ToString();

    private static string FormatDirectionScale(int value)
    {
        if (value > 0)
        {
            return $"{FormatSigned(value)} / +50";
        }

        if (value < 0)
        {
            return $"{value} / -50";
        }

        return "0 / +/-50";
    }

    private static string ValueOrFallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Encode(string value) => Html.Encode(value);

    private static string EncodeMultiline(string value) =>
        Html.Encode(value).Replace("\r\n", "<br>").Replace("\n", "<br>");

    private sealed record DirectionDisplay(
        string Label,
        string SignalLabel,
        string AccentColor,
        string BadgeBackground,
        string BadgeColor,
        string BorderColor);
}
