using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Aeroway.Models;

namespace Aeroway.Helpers;

public static class TestTargetRowFormatter
{
    public static int ComputeNameWidth(IEnumerable<TestTargetRow> rows) =>
        rows.Select(r => r.Name.Length).DefaultIfEmpty(10).Max();

    public static List<TestTargetRow> DedupeByKey(IReadOnlyList<TestTargetRow> targets)
    {
        var map = new Dictionary<string, TestTargetRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in targets)
            map[NormalizeKey(row.Name)] = row;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<TestTargetRow>();
        foreach (var row in targets)
        {
            var key = NormalizeKey(row.Name);
            if (!seen.Add(key)) continue;
            result.Add(map[key]);
        }

        return result;
    }

    private static string NormalizeKey(string name) =>
        name.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    public static void ApplyRowInlines(TextBlock block, TestTargetRow row, int nameWidth)
    {
        block.Inlines.Clear();
        block.FontFamily = TerminalFonts.Mono;
        block.FontSize = TerminalFonts.Size;
        block.TextWrapping = TextWrapping.NoWrap;
        TerminalFonts.ApplyDisplayMode(block);

        block.Inlines.Add(new Run(row.Name.PadRight(nameWidth))
        {
            Foreground = (Brush)Application.Current.FindResource("TextBrush")
        });

        if (row.PingOnly)
        {
            block.Inlines.Add(new Run(" | " + FormatPingLabel())
            {
                Foreground = (Brush)Application.Current.FindResource("TextMutedBrush")
            });
            block.Inlines.Add(new Run(FormatPingValue(row.Ping))
            {
                Foreground = PingBrush(row.Ping)
            });
            return;
        }

        AppendToken(block, row.Http);
        AppendToken(block, row.Tls12);
        AppendToken(block, row.Tls13);
        block.Inlines.Add(new Run(" | " + FormatPingLabel())
        {
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush")
        });
        block.Inlines.Add(new Run(FormatPingValue(row.Ping))
        {
            Foreground = PingBrush(row.Ping)
        });
    }

    private static void AppendToken(TextBlock block, string rawToken)
    {
        block.Inlines.Add(new Run(" | ")
        {
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush")
        });
        block.Inlines.Add(new Run(FormatProtocolToken(rawToken))
        {
            Foreground = TokenBrush(rawToken)
        });
    }

    public static string FormatProtocolToken(string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken is "…")
            return "…";

        return TestOutputLocalizer.TranslateToken(NormalizeToken(rawToken.Trim()));
    }

    private static string NormalizeToken(string token)
    {
        if (token.Contains(':', StringComparison.Ordinal))
            return token;

        return token switch
        {
            "OK" => "HTTP:OK",
            "FAIL" or "ERROR" or "ERR" => "HTTP:ERROR",
            "UNSUP" or "UNSUPPORTED" => "HTTP:UNSUP",
            _ => $"HTTP:{token}"
        };
    }

    public static string FormatPingLabel() =>
        TestOutputLocalizer.IsActive ? "Пинг:" : "Ping:";

    public static string FormatPingValue(string ping)
    {
        if (string.IsNullOrWhiteSpace(ping) || ping is "…")
            return "…";
        return TestOutputLocalizer.TranslatePing(ping);
    }

    public static Brush TokenBrush(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token is "…")
            return (Brush)Application.Current.FindResource("TextMutedBrush");

        var t = token.ToUpperInvariant();
        if (t.Contains("OK") && !t.Contains("UNSUP") && !t.Contains("НЕПОДД"))
            return (Brush)Application.Current.FindResource("SuccessBrush");
        if (t.Contains("UNSUP") || t.Contains("НЕПОДД"))
            return (Brush)Application.Current.FindResource("WarningBrush");
        if (t.Contains("BLOCKED"))
            return (Brush)Application.Current.FindResource("WarningBrush");
        if (t.Contains("ERR") || t.Contains("FAIL") || t.Contains("SSL") || t.Contains("ОШ"))
            return (Brush)Application.Current.FindResource("ErrorBrush");
        return (Brush)Application.Current.FindResource("TextMutedBrush");
    }

    public static Brush PingBrush(string? ping)
    {
        if (string.IsNullOrWhiteSpace(ping) || ping is "…")
            return (Brush)Application.Current.FindResource("TextMutedBrush");
        if (ping.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
            || ping.Contains("Таймаут", StringComparison.OrdinalIgnoreCase))
            return (Brush)Application.Current.FindResource("WarningBrush");
        return new SolidColorBrush(Color.FromRgb(0x5C, 0xD4, 0xD4));
    }
}
