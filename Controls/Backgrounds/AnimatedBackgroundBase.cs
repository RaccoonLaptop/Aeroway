using System.Windows;
using System.Windows.Media;

namespace Aeroway.Controls.Backgrounds;

/// <summary>
/// Фон v1.2.6: кадровая физика. Цикл отрисовки ведёт <see cref="HomeBackgroundHost"/>.
/// Скорость — только через статический <see cref="GlobalSpeed"/> (ползунок на «Главной»).
/// </summary>
public abstract class AnimatedBackgroundBase : FrameworkElement
{
    /// <summary>Множитель скорости; читается каждый кадр из ползунка.</summary>
    public static double GlobalSpeed { get; set; } = 1.0;

    protected double Speed => GlobalSpeed;

    protected double AreaWidth;
    protected double AreaHeight;
    protected readonly Random Rng = new();

    protected AnimatedBackgroundBase()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsHitTestVisible = false;
        ClipToBounds = true;
        SizeChanged += (_, _) => UpdateDimensions();
    }

    public void SetMotionSpeed(double speed) => GlobalSpeed = Math.Clamp(speed, 0.1, 6.0);

    protected double ScaledTimeSec(double timeMs) => timeMs / 1000.0 * Speed;

    internal void Step(double timeMs)
    {
        _lastTimeMs = timeMs;
        if (ActualWidth <= 1 && ActualHeight <= 1)
            UpdateDimensions();
        if (AreaWidth <= 1 || AreaHeight <= 1) return;

        AnimateFrame(timeMs);
    }

    protected void UpdateDimensions()
    {
        AreaWidth = ActualWidth > 1 ? ActualWidth : 800;
        AreaHeight = ActualHeight > 1 ? ActualHeight : 600;
        OnDimensionsChanged();
    }

    protected virtual void OnDimensionsChanged() { }

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    protected override Size ArrangeOverride(Size finalSize)
    {
        AreaWidth = finalSize.Width > 1 ? finalSize.Width : AreaWidth;
        AreaHeight = finalSize.Height > 1 ? finalSize.Height : AreaHeight;
        return finalSize;
    }

    protected virtual void AnimateFrame(double timeMs) { }

    private double _lastTimeMs;

    protected override void OnRender(DrawingContext dc) => RenderFrame(dc, _lastTimeMs);

    protected abstract void RenderFrame(DrawingContext dc, double timeMs);

    protected static Color ParseColor(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;

    protected static void DrawCircle(DrawingContext dc, Color color, double opacity, Point center, double radius)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), color.R, color.G, color.B));
        brush.Freeze();
        dc.DrawEllipse(brush, null, center, radius, radius);
    }
}
