using System.Globalization;

namespace ZapretUI.Controls.Backgrounds;

/// <summary>
/// Верх шкалы диаграммы. Пока скорость ниже ступени, потолок остаётся на ней.
/// Достигла 100 Мбит — потолок становится 250 Мбит.
/// </summary>
public static class BandwidthScale
{
    private static readonly double[] Megabits = [10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000];

    public static double CeilingMegabits(double peakMegabits)
    {
        var peak = Math.Max(0, peakMegabits);
        foreach (var step in Megabits)
        {
            if (peak < step)
                return step;
        }

        return Megabits[^1];
    }

    public static double MegabitsFromBytes(double bytesPerSecond) =>
        Math.Max(0, bytesPerSecond) * 8d / 1_000_000d;

    public static string Format(double megabits)
    {
        if (megabits >= 1000)
        {
            var gigabits = megabits / 1000d;
            var text = Math.Abs(gigabits - Math.Round(gigabits)) < 0.05
                ? Math.Round(gigabits).ToString("0", CultureInfo.InvariantCulture)
                : gigabits.ToString("0.#", CultureInfo.InvariantCulture);
            return text + " Гбит";
        }

        return megabits.ToString("0", CultureInfo.InvariantCulture) + " Мбит";
    }

    public static string FormatLive(double megabits)
    {
        var value = Math.Max(0, megabits);
        if (value >= 1000)
            return Format(value);
        if (value >= 100)
            return value.ToString("0", CultureInfo.InvariantCulture) + " Мбит";
        return value.ToString("0.#", CultureInfo.InvariantCulture) + " Мбит";
    }
}
