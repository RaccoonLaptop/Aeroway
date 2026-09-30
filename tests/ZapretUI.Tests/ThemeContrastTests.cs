using ZapretUI.Services;
using Xunit;

namespace ZapretUI.Tests;

public class ThemeContrastTests
{
    [Fact]
    public void Light_secondary_text_meets_wcag_aa_at_12px()
    {
        var ratio = Contrast(ThemeService.LightMuted, ThemeService.LightPaper);
        Assert.True(ratio >= 4.5, $"contrast {ratio:0.00} is below 4.5:1");
    }

    private static double Contrast(System.Windows.Media.Color fore, System.Windows.Media.Color back)
    {
        var lighter = Math.Max(Luminance(fore), Luminance(back));
        var darker = Math.Min(Luminance(fore), Luminance(back));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(System.Windows.Media.Color color)
    {
        double Channel(byte value)
        {
            var s = value / 255d;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }
}
