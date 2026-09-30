using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ZapretUI.Controls.Backgrounds;

/// <summary>
/// Узкая диаграмма скорости внизу фона. Ширина и высота берутся из текущего окна.
/// </summary>
public static class RateChart
{
    public static double MenuInset { get; set; } = 248;

    public static void Draw(
        DrawingContext dc,
        double width,
        double height,
        double pixelsPerDip,
        ReadOnlySpan<FlowSample> samples,
        long now,
        bool flashing,
        double leftInset)
    {
        if (width < leftInset + 160 || height < 80)
            return;

        var band = Math.Clamp(height * 0.1, 54, 78);
        var plotLeft = leftInset + 10;
        var plotRight = width - 78;
        var plotBottom = height - 18;
        var plotTop = height - band + 6;
        if (plotRight - plotLeft < 40 || plotBottom - plotTop < 16)
            return;

        double peakBytes = 0;
        double currentBytes = 0;
        for (var i = 0; i < samples.Length; i++)
        {
            var age = now - samples[i].TickMs;
            if (age < 0 || age > FlowTrace.WindowMs)
                continue;
            currentBytes = samples[i].BytesPerSecond;
            if (currentBytes > peakBytes)
                peakBytes = currentBytes;
        }

        var ceilingMegabits = BandwidthScale.CeilingMegabits(BandwidthScale.MegabitsFromBytes(peakBytes));
        var ceilingBytes = ceilingMegabits * 1_000_000d / 8d;

        var guide = new Pen(new SolidColorBrush(Color.FromArgb(42, 201, 184, 150)), 1);
        guide.Freeze();
        dc.DrawLine(guide, new Point(plotLeft, plotTop), new Point(plotRight, plotTop));
        dc.DrawLine(guide, new Point(plotLeft, plotBottom), new Point(plotRight, plotBottom));

        var label = new SolidColorBrush(Color.FromArgb(120, 201, 184, 150));
        label.Freeze();
        DrawText(dc, BandwidthScale.Format(ceilingMegabits), plotRight + 8, plotTop - 7, 11, label, pixelsPerDip);
        DrawText(dc, "0", plotRight + 8, plotBottom - 7, 11, label, pixelsPerDip);
        var live = new SolidColorBrush(Color.FromArgb(210, 244, 231, 198));
        live.Freeze();
        var liveText = CreateText(BandwidthScale.FormatLive(BandwidthScale.MegabitsFromBytes(currentBytes)), 13, live, pixelsPerDip);
        var liveX = (plotLeft + plotRight) / 2 + 36 - liveText.Width / 2;
        var liveY = (plotTop + plotBottom) / 2 - liveText.Height / 2;
        dc.DrawText(liveText, new Point(liveX, liveY));
        DrawText(dc, "60 с", plotLeft, plotBottom + 1, 11, label, pixelsPerDip);
        DrawText(dc, "сейчас", plotRight - 38, plotBottom + 1, 11, label, pixelsPerDip);

        if (samples.Length == 0 || ceilingBytes <= 0)
            return;

        var line = new StreamGeometry();
        var fill = new StreamGeometry();
        var started = false;
        double lastX = 0;
        using (var lineCtx = line.Open())
        using (var fillCtx = fill.Open())
        {
            for (var i = 0; i < samples.Length; i++)
            {
                var age = now - samples[i].TickMs;
                if (age < 0 || age > FlowTrace.WindowMs)
                    continue;
                var x = plotRight - age / (double)FlowTrace.WindowMs * (plotRight - plotLeft);
                var unit = Math.Clamp(samples[i].BytesPerSecond / ceilingBytes, 0, 1);
                var y = plotBottom - unit * (plotBottom - plotTop);
                if (!started)
                {
                    lineCtx.BeginFigure(new Point(x, y), false, false);
                    fillCtx.BeginFigure(new Point(x, plotBottom), true, true);
                    fillCtx.LineTo(new Point(x, y), true, false);
                    started = true;
                }
                else
                {
                    lineCtx.LineTo(new Point(x, y), true, true);
                    fillCtx.LineTo(new Point(x, y), true, false);
                }

                lastX = x;
            }

            if (started)
                fillCtx.LineTo(new Point(lastX, plotBottom), true, false);
        }

        if (!started)
            return;

        fill.Freeze();
        line.Freeze();
        var wash = new SolidColorBrush(flashing
            ? Color.FromArgb(48, 255, 214, 90)
            : Color.FromArgb(36, 224, 178, 64));
        wash.Freeze();
        dc.DrawGeometry(wash, null, fill);
        var stroke = new Pen(new SolidColorBrush(flashing
            ? Color.FromArgb(220, 255, 220, 120)
            : Color.FromArgb(190, 224, 178, 64)), 1.4);
        stroke.Freeze();
        dc.DrawGeometry(null, stroke, line);
    }

    private static void DrawText(
        DrawingContext dc,
        string text,
        double x,
        double y,
        double size,
        Brush brush,
        double pixelsPerDip)
    {
        dc.DrawText(CreateText(text, size, brush, pixelsPerDip), new Point(x, y));
    }

    private static FormattedText CreateText(string text, double size, Brush brush, double pixelsPerDip) =>
        new(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            size,
            brush,
            pixelsPerDip);
}
