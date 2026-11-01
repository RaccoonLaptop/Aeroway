using System.Windows;
using System.Windows.Media;

namespace Aeroway.Helpers;

/// <summary>
/// Monospace family with Cyrillic. Consolas-only on Windows 11 falls back
/// per glyph and colored Runs overlap.
/// </summary>
internal static class TerminalFonts
{
    public const double Size = 12.5;

    public static readonly FontFamily Mono = new(
        "Cascadia Mono, Cascadia Code, Consolas, Courier New");

    public static void ApplyDisplayMode(DependencyObject target)
    {
        TextOptions.SetTextFormattingMode(target, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(target, TextRenderingMode.ClearType);
        TextOptions.SetTextHintingMode(target, TextHintingMode.Fixed);
    }
}
