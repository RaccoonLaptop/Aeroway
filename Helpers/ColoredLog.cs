using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Aeroway.Helpers;

public static class ColoredLog
{
    private static readonly Regex ColorTag = new(@"\{COLOR:(\w+)\}(.*)", RegexOptions.Compiled);

    public static void AppendParagraph(RichTextBox box, string line)
    {
        var brush = BrushForLine(line);
        var content = StripColorTag(line);
        box.Document.Blocks.Add(new Paragraph(new Run(content))
        {
            Foreground = brush,
            Margin = new Thickness(0),
            LineHeight = 1
        });
        box.ScrollToEnd();
    }

    public static void SetDocumentText(RichTextBox box, string text)
    {
        box.Document.Blocks.Clear();
        foreach (var line in text.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (string.IsNullOrEmpty(line)) continue;
            AppendParagraph(box, line);
        }
    }

    public static string StripColorTag(string line)
    {
        var m = ColorTag.Match(line);
        return m.Success ? m.Groups[2].Value : line;
    }

    public static Brush BrushForLine(string line)
    {
        var m = ColorTag.Match(line);
        if (m.Success)
            return BrushFromName(m.Groups[1].Value);

        var lower = line.ToLowerInvariant();
        if (lower.Contains("[ok]") || lower.Contains("running") || lower.Contains("success") ||
            lower.Contains("актуал") || lower.Contains("готово") || lower.Contains("complete") ||
            lower.Contains("installed successfully") || lower.Contains("up to date"))
            return GetBrush("SuccessBrush");

        if (lower.Contains("[x]") || lower.Contains("not running") || lower.Contains("not installed") ||
            lower.Contains("not found") || lower.Contains("ошибка") || lower.Contains("error"))
            return GetBrush("ErrorBrush");

        if (lower.Contains("[?]") || lower.Contains("warning") || lower.Contains("needs update"))
            return GetBrush("WarningBrush");

        if (line.StartsWith("---") || lower.Contains("диагност") || lower.Contains("diagnostics"))
            return GetBrush("AccentBrush");

        return GetBrush("TextBrush");
    }

    private static Brush BrushFromName(string name) => name.ToLowerInvariant() switch
    {
        "green" => GetBrush("SuccessBrush"),
        "red" => GetBrush("ErrorBrush"),
        "yellow" => GetBrush("WarningBrush"),
        "cyan" => GetBrush("AccentBrush"),
        "darkgray" or "darkgrey" => GetBrush("TextMutedBrush"),
        _ => GetBrush("TextBrush")
    };

    private static Brush GetBrush(string key) =>
        (Brush)Application.Current.FindResource(key);
}
