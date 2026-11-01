using System.Windows;
using System.Windows.Media;

namespace Aeroway.Controls.Backgrounds;

/// <summary>
/// Как v1.2.6 (bedolaga shooting-stars): пиксели за кадр, не px/sec.
/// </summary>
public sealed class ShootingStarsBackground : AnimatedBackgroundBase
{
    private const double StarDensity = 0.00015;
    private const double MinSpeed = 10;
    private const double MaxSpeed = 30;
    private const double TrailLength = 80;
    private const double FadeDistance = 500;

    private static readonly Color StarColor = ParseColor("#9E00FF");
    private static readonly Color TrailColor = ParseColor("#2EB9DF");

    private readonly List<BgStar> _bgStars = new();
    private readonly List<ShootingStar> _shootingStars = new();
    private double _lastShootingMs;
    private double _nextShootingDelayMs = 4200;

    private sealed class BgStar
    {
        public double X, Y, Radius, Opacity;
        public double? TwinkleSpeed;
    }

    private sealed class ShootingStar
    {
        public double X, Y, Angle, Scale, Speed, Distance, Opacity;
    }

    protected override void OnDimensionsChanged() => RebuildStars();

    protected override void AnimateFrame(double timeMs)
    {
        if (AreaWidth <= 0 || AreaHeight <= 0) return;

        if (timeMs - _lastShootingMs > _nextShootingDelayMs / Speed)
        {
            _shootingStars.Add(new ShootingStar
            {
                X = Rng.NextDouble() * AreaWidth,
                Y = Rng.NextDouble() * AreaHeight * 0.5,
                Angle = Math.PI / 4 + (Rng.NextDouble() - 0.5) * 0.3,
                Scale = 0.5 + Rng.NextDouble() * 0.5,
                Speed = MinSpeed + Rng.NextDouble() * (MaxSpeed - MinSpeed),
                Distance = 0,
                Opacity = 1
            });
            _lastShootingMs = timeMs;
            _nextShootingDelayMs = 4200 + Rng.NextDouble() * 4500;
        }

        for (var i = _shootingStars.Count - 1; i >= 0; i--)
        {
            var star = _shootingStars[i];
            star.Distance += star.Speed * Speed;
            star.Opacity = Math.Max(0, 1 - star.Distance / FadeDistance);
            if (star.Opacity <= 0) _shootingStars.RemoveAt(i);
        }
    }

    protected override void RenderFrame(DrawingContext dc, double timeMs)
    {
        if (AreaWidth <= 0 || AreaHeight <= 0) return;
        var timeSec = ScaledTimeSec(timeMs);

        foreach (var s in _bgStars)
        {
            var opacity = s.Opacity;
            if (s.TwinkleSpeed is { } speed)
                opacity = 0.5 + 0.5 * Math.Sin(timeSec * speed * Math.PI * 2);
            DrawCircle(dc, Colors.White, opacity, new Point(s.X, s.Y), s.Radius);
        }

        foreach (var star in _shootingStars)
        {
            var x2 = star.X + Math.Cos(star.Angle) * star.Distance;
            var y2 = star.Y + Math.Sin(star.Angle) * star.Distance;
            var tailX = star.X + Math.Cos(star.Angle) * Math.Max(0, star.Distance - TrailLength);
            var tailY = star.Y + Math.Sin(star.Angle) * Math.Max(0, star.Distance - TrailLength);

            var trailBrush = new SolidColorBrush(Color.FromArgb(
                (byte)(star.Opacity * 0.4 * 255), TrailColor.R, TrailColor.G, TrailColor.B));
            trailBrush.Freeze();
            var pen = new Pen(trailBrush, star.Scale * 2)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            pen.Freeze();
            dc.DrawLine(pen, new Point(tailX, tailY), new Point(x2, y2));
            DrawCircle(dc, StarColor, star.Opacity, new Point(x2, y2), star.Scale * 1.5);
        }
    }

    private void RebuildStars()
    {
        _bgStars.Clear();
        if (AreaWidth <= 0 || AreaHeight <= 0) return;
        var count = Math.Max(1, (int)(AreaWidth * AreaHeight * StarDensity));
        for (var i = 0; i < count; i++)
        {
            _bgStars.Add(new BgStar
            {
                X = Rng.NextDouble() * AreaWidth,
                Y = Rng.NextDouble() * AreaHeight,
                Radius = Rng.NextDouble() * 1.2 + 0.3,
                Opacity = Rng.NextDouble(),
                TwinkleSpeed = Rng.NextDouble() > 0.3 ? 0.5 + Rng.NextDouble() * 0.5 : null
            });
        }
    }
}
