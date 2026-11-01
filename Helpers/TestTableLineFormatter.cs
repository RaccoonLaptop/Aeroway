using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aeroway.Models;

namespace Aeroway.Helpers;

internal sealed class TestTableRow
{
    public required string Name { get; init; }
    public string? Http { get; init; }
    public string? Tls12 { get; init; }
    public string? Tls13 { get; init; }
    public required string Ping { get; init; }
    public bool PingOnly => Http is null;
}

internal sealed class AnalyticsTableRow
{
    public required string Config { get; init; }
    public required string HttpOk { get; init; }
    public required string Err { get; init; }
    public required string Unsup { get; init; }
    public required string PingOk { get; init; }
    public required string Fail { get; init; }
}

/// <summary>
/// Buffers table rows and renders with dynamic column widths:
/// 3-space indent + name pad | col1 pad | col2 pad | ...
/// </summary>
internal sealed class TerminalTableBuffer
{
    private const string Indent = "   ";

    private readonly List<TestTableRow> _testRows = [];
    private readonly List<AnalyticsTableRow> _analyticsRows = [];

    public void Clear()
    {
        _testRows.Clear();
        _analyticsRows.Clear();
    }

    public bool TryBufferLine(string plainLine)
    {
        if (TestTableLineFormatter.TryParseTestRow(plainLine, out var testRow))
        {
            _testRows.Add(testRow);
            return true;
        }

        if (TestTableLineFormatter.TryParseAnalyticsRow(plainLine, out var analyticsRow))
        {
            _analyticsRows.Add(analyticsRow);
            return true;
        }

        return false;
    }

    public void FlushBeforeNonTableLine(RichTextBox box, AnsiTerminalRenderer renderer)
    {
        FlushTestTable(box, renderer);
        FlushAnalyticsTable(box, renderer);
    }

    public void FlushAll(RichTextBox box, AnsiTerminalRenderer renderer)
    {
        FlushTestTable(box, renderer);
        FlushAnalyticsTable(box, renderer);
    }

    private void FlushTestTable(RichTextBox box, AnsiTerminalRenderer renderer)
    {
        if (_testRows.Count == 0) return;

        var targets = _testRows.Select(row => new TestTargetRow
        {
            Name = row.Name,
            Http = row.Http ?? "…",
            Tls12 = row.Tls12 ?? "…",
            Tls13 = row.Tls13 ?? "…",
            Ping = row.Ping,
            PingOnly = row.PingOnly
        }).ToList();

        var host = new StackPanel { Orientation = Orientation.Vertical };
        TestTargetTableRenderer.Render(host, targets);
        renderer.AppendUi(box, host);
        _testRows.Clear();
    }

    private void FlushAnalyticsTable(RichTextBox box, AnsiTerminalRenderer renderer)
    {
        if (_analyticsRows.Count == 0) return;

        var displayRows = _analyticsRows.Select(TestOutputLocalizer.ToDisplayRow).ToList();
        var configW = displayRows.Max(r => r.Config.Length);
        var httpOkW = displayRows.Max(r => r.HttpOk.Length);
        var errW = displayRows.Max(r => r.Err.Length);
        var unsupW = displayRows.Max(r => r.Unsup.Length);
        var pingOkW = displayRows.Max(r => r.PingOk.Length);
        var failW = displayRows.Max(r => r.Fail.Length);

        foreach (var row in displayRows)
        {
            var segments = new List<(string Text, Brush Foreground)>
            {
                (Indent + row.Config.PadRight(configW), TestTableLineFormatter.AnalyticsBrush()),
                (" | " + row.HttpOk.PadRight(httpOkW), TestTableLineFormatter.AnalyticsBrush()),
                (" | " + row.Err.PadRight(errW), TestTableLineFormatter.AnalyticsBrush()),
                (" | " + row.Unsup.PadRight(unsupW), TestTableLineFormatter.AnalyticsBrush()),
                (" | " + row.PingOk.PadRight(pingOkW), TestTableLineFormatter.AnalyticsBrush()),
                (" | " + row.Fail.PadRight(failW), TestTableLineFormatter.AnalyticsBrush())
            };
            renderer.AppendFormattedLine(box, segments);
        }

        _analyticsRows.Clear();
    }
}

internal static class TestTableLineFormatter
{
    private static readonly Regex UrlTestRow = new(
        @"^\s*(?<name>[A-Za-z][A-Za-z0-9_]*)\s*(?<http>HTTP:\S+)\s*(?<tls12>TLS1\.2:\S+)\s*(?<tls13>TLS1\.3:\S+)\s*\|\s*(?:Ping|Пинг):\s*(?<ping>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PingOnlyRow = new(
        @"^\s*(?<name>[A-Za-z][A-Za-z0-9_]*)\s*(?:Ping|Пинг):\s*(?<ping>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GluedProtocol = new(
        @"(?<=\w)(?=HTTP:|TLS1\.|Ping:|Пинг:)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StandardAnalyticsRow = new(
        @"^(?<config>.+?)\s*:\s*HTTP OK:\s*(?<ok>\d+)\s*,\s*ERR:\s*(?<err>\d+)\s*,\s*UNSUP:\s*(?<unsup>\d+)\s*,\s*Ping OK:\s*(?<pingOk>\d+)\s*,\s*Fail:\s*(?<fail>\d+)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsAnalyticsHeader(string plainLine) =>
        plainLine.Contains("=== ANALYTICS ===", StringComparison.Ordinal);

    public static bool ShouldFlushTestTable(string plainLine) =>
        IsAnalyticsHeader(plainLine)
        || plainLine.Contains("=== АНАЛИТИКА ===", StringComparison.Ordinal)
        || ConfigHeader.IsMatch(plainLine)
        || plainLine.Contains("All tests finished", StringComparison.OrdinalIgnoreCase)
        || plainLine.Contains("Все тесты завершены", StringComparison.OrdinalIgnoreCase);

    private static readonly Regex ConfigHeader = new(
        @"^\s*\[\d+/\d+\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParseTestRow(string plainLine, out TestTableRow row)
    {
        row = null!;
        var line = NormalizeParseLine(plainLine);
        if (line.Length == 0) return false;

        var m = UrlTestRow.Match(line);
        if (m.Success)
        {
            row = new TestTableRow
            {
                Name = m.Groups["name"].Value.Trim(),
                Http = CanonicalToken(m.Groups["http"].Value),
                Tls12 = CanonicalToken(m.Groups["tls12"].Value),
                Tls13 = CanonicalToken(m.Groups["tls13"].Value),
                Ping = CanonicalPing(m.Groups["ping"].Value)
            };
            return true;
        }

        if (!line.Contains("HTTP:", StringComparison.Ordinal)
            && !line.Contains("TLS1.2:", StringComparison.Ordinal))
        {
            m = PingOnlyRow.Match(line);
            if (m.Success)
            {
                row = new TestTableRow
                {
                    Name = m.Groups["name"].Value.Trim(),
                    Ping = CanonicalPing(m.Groups["ping"].Value)
                };
                return true;
            }
        }

        return false;
    }

    private static string NormalizeParseLine(string plainLine)
    {
        var line = plainLine.Replace('\u00A0', ' ').Trim();
        if (line.Length == 0) return line;
        return GluedProtocol.Replace(line, " ");
    }

    private static string CanonicalToken(string token)
    {
        var value = NormalizeToken(token).Replace(" ", "", StringComparison.Ordinal);
        return value
            .Replace("ОШИБКА", "ERROR", StringComparison.Ordinal)
            .Replace("НЕПОДД", "UNSUP", StringComparison.Ordinal)
            .Replace("БЛОК", "BLOCKED", StringComparison.Ordinal)
            .Replace("ОК", "OK", StringComparison.Ordinal);
    }

    private static string CanonicalPing(string ping) =>
        ping.Replace('\u00A0', ' ').Trim()
            .Replace("Таймаут", "Timeout", StringComparison.OrdinalIgnoreCase)
            .Replace(" мс", " ms", StringComparison.Ordinal);

    public static bool TryParseAnalyticsRow(string plainLine, out AnalyticsTableRow row)
    {
        row = null!;
        var m = StandardAnalyticsRow.Match(plainLine);
        if (!m.Success) return false;

        row = new AnalyticsTableRow
        {
            Config = m.Groups["config"].Value.Trim(),
            HttpOk = $"HTTP OK: {m.Groups["ok"].Value}",
            Err = $"ERR: {m.Groups["err"].Value}",
            Unsup = $"UNSUP: {m.Groups["unsup"].Value}",
            PingOk = $"Ping OK: {m.Groups["pingOk"].Value}",
            Fail = $"Fail: {m.Groups["fail"].Value}"
        };
        return true;
    }

    private static string NormalizeToken(string token) => token.TrimEnd();

    public static Brush TokenBrush(string token)
    {
        var t = NormalizeToken(token);
        if (t.Contains("UNSUP", StringComparison.OrdinalIgnoreCase)
            || t.Contains("НЕПОДД", StringComparison.OrdinalIgnoreCase))
            return GetBrush("WarningBrush");
        if (t.Contains("ERR", StringComparison.OrdinalIgnoreCase)
            || t.Contains("ОШИБКА", StringComparison.OrdinalIgnoreCase)
            || t.Contains("ОШ:", StringComparison.OrdinalIgnoreCase)
            || t.Contains("SSL", StringComparison.OrdinalIgnoreCase))
            return GetBrush("ErrorBrush");
        return GetBrush("SuccessBrush");
    }

    public static Brush PingBrush(string ping) =>
        ping.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
            || ping.Contains("Таймаут", StringComparison.OrdinalIgnoreCase)
            ? GetBrush("WarningBrush")
            : new SolidColorBrush(Color.FromRgb(0x5C, 0xD4, 0xD4));

    public static Brush TextBrush() => GetBrush("TextBrush");
    public static Brush MutedBrush() => GetBrush("TextMutedBrush");
    public static Brush AnalyticsBrush() => GetBrush("WarningBrush");

    private static Brush GetBrush(string key) =>
        (Brush)Application.Current.FindResource(key);
}
