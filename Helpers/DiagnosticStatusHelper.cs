using System.Windows;
using System.Windows.Media;

namespace Aeroway.Helpers;

public static class DiagnosticStatusHelper
{
    public static string ToDisplayText(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token is "…" or "-")
            return "…";

        var t = token.Trim();
        if (t.Contains("OK", StringComparison.OrdinalIgnoreCase))
            return "OK";
        if (t.Contains("UNSUP", StringComparison.OrdinalIgnoreCase)
            || t.Contains("НЕПОДД", StringComparison.OrdinalIgnoreCase)
            || t.Contains("UNSUPPORTED", StringComparison.OrdinalIgnoreCase))
            return "N/A";
        if (t.Contains("BLOCKED", StringComparison.OrdinalIgnoreCase)
            || t.Contains("LIKELY", StringComparison.OrdinalIgnoreCase))
            return "BLOCK";
        if (t.Contains("ERR", StringComparison.OrdinalIgnoreCase)
            || t.Contains("ОШИБКА", StringComparison.OrdinalIgnoreCase)
            || t.Contains("FAIL", StringComparison.OrdinalIgnoreCase)
            || t.Contains("SSL", StringComparison.OrdinalIgnoreCase))
            return "FAIL";
        if (t.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Таймаут", StringComparison.OrdinalIgnoreCase))
            return "TIMEOUT";

        return t;
    }

    public static Brush ToBrush(string? token)
    {
        return ToDisplayText(token) switch
        {
            "OK" => GetBrush("SuccessBrush"),
            "FAIL" or "BLOCK" => GetBrush("ErrorBrush"),
            "TIMEOUT" or "N/A" => GetBrush("WarningBrush"),
            _ => GetBrush("TextMutedBrush")
        };
    }

    private static Brush GetBrush(string key) =>
        (Brush)Application.Current.FindResource(key);
}
