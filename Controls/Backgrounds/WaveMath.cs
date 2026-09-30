namespace ZapretUI.Controls.Backgrounds;

/// <summary>
/// Высота гребня и нижней линии. Flow больше 1 — это уже полный канал:
/// 100 и 1000 дают ту же высоту, что и 1, иначе формула 10 + flow * 120
/// раздувает волну на весь экран.
/// </summary>
public static class WaveMath
{
    public static double ClampFlow(double flow) => Math.Clamp(flow, 0, 1);

    public static double Crest(double flow, double milliseconds, bool animate)
    {
        var shown = ClampFlow(flow);
        var crest = 10 + shown * 120;
        if (!animate)
            return crest;

        var pump = 0.72 + 0.28 * Math.Sin(milliseconds / 700.0);
        return crest * pump;
    }

    public static double Line(double flow, double milliseconds, bool animate)
    {
        if (ClampFlow(flow) <= 0)
            return 1.5;
        return 4 + Crest(flow, milliseconds, animate) * 0.42;
    }
}
