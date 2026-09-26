using System.Windows;
using System.Windows.Media;

namespace ZapretUI.Controls.Backgrounds;

public sealed class MeteorsBackground : AnimatedBackgroundBase
{
    private readonly List<Meteor> _meteors = new();
    private double _spawnAccum;

    private sealed class Meteor
    {
        public double X, Y, Angle, Speed, Length, Opacity, Phase;
    }

    protected override void OnDimensionsChanged()
    {
        _meteors.Clear();
        var count = Math.Clamp((int)(AreaWidth * AreaHeight / 25000), 12, 28);
        for (var i = 0; i < count; i++)
            _meteors.Add(CreateMeteor(randomPhase: true));
    }

    protected override void AnimateFrame(double timeMs)
    {
        _spawnAccum += 16 * Speed;
        if (_spawnAccum > 400 && _meteors.Count < 35)
        {
            _meteors.Add(CreateMeteor());
            _spawnAccum = 0;
        }

        for (var i = _meteors.Count - 1; i >= 0; i--)
        {
            var m = _meteors[i];
            m.Phase += m.Speed * Speed;
            m.Opacity = Math.Max(0, 1 - m.Phase / 900);
            if (m.Opacity <= 0 || m.X > AreaWidth + 200 || m.Y > AreaHeight + 200)
                _meteors.RemoveAt(i);
        }
    }

    protected override void RenderFrame(DrawingContext dc, double timeMs)
    {
        foreach (var m in _meteors)
        {
            var head = new Point(m.X + Math.Cos(m.Angle) * m.Phase, m.Y + Math.Sin(m.Angle) * m.Phase);
            var tail = new Point(m.X + Math.Cos(m.Angle) * (m.Phase - m.Length), m.Y + Math.Sin(m.Angle) * (m.Phase - m.Length));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(m.Opacity * 200), 255, 255, 255)), 1.5)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            pen.Freeze();
            dc.DrawLine(pen, tail, head);
        }
    }

    private Meteor CreateMeteor(bool randomPhase = false)
    {
        var angle = Math.PI / 4 + (Rng.NextDouble() - 0.5) * 0.15;
        return new Meteor
        {
            X = Rng.NextDouble() * AreaWidth * 1.2 - AreaWidth * 0.1,
            Y = Rng.NextDouble() * AreaHeight * 0.55,
            Angle = angle,
            Speed = 4 + Rng.NextDouble() * 6,
            Length = 60 + Rng.NextDouble() * 80,
            Opacity = 1,
            Phase = randomPhase ? Rng.NextDouble() * 400 : 0
        };
    }
}

public sealed class SparklesBackground : AnimatedBackgroundBase
{
    private readonly List<Particle> _particles = new();

    private sealed class Particle
    {
        public double X, Y, Size, SpeedX, SpeedY, Opacity, OpacityDir, OpacitySpeed;
    }

    protected override void OnDimensionsChanged()
    {
        _particles.Clear();
        if (AreaWidth <= 0 || AreaHeight <= 0) return;
        var count = Math.Clamp((int)(AreaWidth * AreaHeight / 8000), 60, 400);
        for (var i = 0; i < count; i++)
            _particles.Add(CreateParticle());
    }

    protected override void AnimateFrame(double timeMs)
    {
        foreach (var p in _particles)
        {
            p.X += p.SpeedX * Speed;
            p.Y += p.SpeedY * Speed;
            p.Opacity += p.OpacityDir * p.OpacitySpeed * Speed;
            if (p.Opacity <= 0) { p.Opacity = 0; p.OpacityDir = 1; }
            else if (p.Opacity >= 1) { p.Opacity = 1; p.OpacityDir = -1; }
            if (p.X < 0) p.X = AreaWidth;
            if (p.X > AreaWidth) p.X = 0;
            if (p.Y < 0) p.Y = AreaHeight;
            if (p.Y > AreaHeight) p.Y = 0;
        }
    }

    protected override void RenderFrame(DrawingContext dc, double timeMs)
    {
        foreach (var p in _particles)
            DrawCircle(dc, Colors.White, p.Opacity * 0.85, new Point(p.X, p.Y), p.Size);
    }

    private Particle CreateParticle() => new()
    {
        X = Rng.NextDouble() * AreaWidth,
        Y = Rng.NextDouble() * AreaHeight,
        Size = 0.4 + Rng.NextDouble() * 1.0,
        SpeedX = (Rng.NextDouble() - 0.5) * 0.4,
        SpeedY = (Rng.NextDouble() - 0.5) * 0.4,
        Opacity = Rng.NextDouble(),
        OpacityDir = Rng.NextDouble() > 0.5 ? 1 : -1,
        OpacitySpeed = 0.004 + Rng.NextDouble() * 0.01
    };
}

public sealed class AuroraBackground : AnimatedBackgroundBase
{
    protected override void RenderFrame(DrawingContext dc, double timeMs)
    {
        if (AreaWidth <= 0 || AreaHeight <= 0) return;
        var t = ScaledTimeSec(timeMs);
        var colors = new[] { ParseColor("#00d2ff"), ParseColor("#7928ca"), ParseColor("#ff0080") };
        for (var i = 0; i < 3; i++)
        {
            var cx = AreaWidth * (0.3 + 0.2 * i) + Math.Sin(t * 0.3 + i) * AreaWidth * 0.15;
            var cy = AreaHeight * 0.5 + Math.Cos(t * 0.25 + i * 1.7) * AreaHeight * 0.2;
            var r = Math.Min(AreaWidth, AreaHeight) * (0.35 + 0.05 * Math.Sin(t + i));
            var brush = new RadialGradientBrush(colors[i], Color.FromArgb(0, 0, 0, 0)) { Opacity = 0.22 };
            brush.Freeze();
            dc.DrawEllipse(brush, null, new Point(cx, cy), r, r * 0.6);
        }
    }
}

public sealed class VortexBackground : AnimatedBackgroundBase
{
    private readonly List<VParticle> _particles = new();

    private sealed class VParticle
    {
        public double Angle, Radius, Speed, Size;
    }

    protected override void OnDimensionsChanged()
    {
        _particles.Clear();
        for (var i = 0; i < 280; i++)
        {
            _particles.Add(new VParticle
            {
                Angle = Rng.NextDouble() * Math.PI * 2,
                Radius = Rng.NextDouble() * Math.Min(AreaWidth, AreaHeight) * 0.45,
                Speed = 0.002 + Rng.NextDouble() * 0.004,
                Size = 0.6 + Rng.NextDouble() * 1.4
            });
        }
    }

    protected override void AnimateFrame(double timeMs)
    {
        foreach (var p in _particles)
            p.Angle += p.Speed * Speed;
    }

    protected override void RenderFrame(DrawingContext dc, double timeMs)
    {
        var cx = AreaWidth / 2;
        var cy = AreaHeight / 2;
        foreach (var p in _particles)
        {
            var x = cx + Math.Cos(p.Angle) * p.Radius;
            var y = cy + Math.Sin(p.Angle) * p.Radius * 0.7;
            var hue = (220 + p.Angle * 40) % 360;
            var color = HsvToRgb(hue, 0.6, 0.9);
            DrawCircle(dc, color, 0.35, new Point(x, y), p.Size);
        }
    }

    private static Color HsvToRgb(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        var m = v - c;
        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}

public sealed class GridBackground : AnimatedBackgroundBase
{
    private readonly bool _dots;
    public GridBackground(bool dots) => _dots = dots;

    protected override void RenderFrame(DrawingContext dc, double timeMs)
    {
        if (AreaWidth <= 0 || AreaHeight <= 0) return;
        var step = _dots ? 24.0 : 40.0;
        var color = Color.FromArgb(_dots ? (byte)28 : (byte)18, 255, 255, 255);

        if (_dots)
        {
            for (var x = 0.0; x < AreaWidth; x += step)
            for (var y = 0.0; y < AreaHeight; y += step)
                DrawCircle(dc, Colors.White, 0.12, new Point(x, y), 1.2);
        }
        else
        {
            var pen = new Pen(new SolidColorBrush(color), 1);
            pen.Freeze();
            for (var x = 0.0; x < AreaWidth; x += step)
                dc.DrawLine(pen, new Point(x, 0), new Point(x, AreaHeight));
            for (var y = 0.0; y < AreaHeight; y += step)
                dc.DrawLine(pen, new Point(0, y), new Point(AreaWidth, y));
        }
    }
}

public sealed class RippleBackground : AnimatedBackgroundBase
{
    private readonly List<Ripple> _ripples = new();

    private sealed class Ripple
    {
        public double X, Y, Radius, Speed, MaxRadius;
    }

    protected override void OnDimensionsChanged() => _ripples.Clear();

    protected override void AnimateFrame(double timeMs)
    {
        if (Rng.NextDouble() < 0.02 * Speed && _ripples.Count < 6)
        {
            _ripples.Add(new Ripple
            {
                X = Rng.NextDouble() * AreaWidth,
                Y = Rng.NextDouble() * AreaHeight,
                Radius = 0,
                Speed = 1.2 + Rng.NextDouble(),
                MaxRadius = 80 + Rng.NextDouble() * 160
            });
        }

        for (var i = _ripples.Count - 1; i >= 0; i--)
        {
            _ripples[i].Radius += _ripples[i].Speed * Speed;
            if (_ripples[i].Radius > _ripples[i].MaxRadius)
                _ripples.RemoveAt(i);
        }
    }

    protected override void RenderFrame(DrawingContext dc, double timeMs)
    {
        foreach (var r in _ripples)
        {
            var opacity = 1 - r.Radius / r.MaxRadius;
            var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(opacity * 80), 224, 178, 64)), 1.5);
            pen.Freeze();
            dc.DrawEllipse(null, pen, new Point(r.X, r.Y), r.Radius, r.Radius);
        }
    }
}

public sealed class WavyBackground : AnimatedBackgroundBase
{
    protected override void RenderFrame(DrawingContext dc, double timeMs)
    {
        if (AreaWidth <= 0 || AreaHeight <= 0) return;
        var menu = Application.Current?.TryFindResource("SurfaceBrush") as SolidColorBrush
                   ?? new SolidColorBrush(Color.FromRgb(0x0D, 0x22, 0x40));
        dc.DrawRectangle(menu, null, new Rect(0, 0, AreaWidth, AreaHeight));
        var t = ScaledTimeSec(timeMs);
        for (var wave = 0; wave < 4; wave++)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(0, AreaHeight), true, true);
                for (var x = 0.0; x <= AreaWidth; x += 8)
                {
                    var y = AreaHeight * (0.35 + wave * 0.12) +
                            Math.Sin(x * 0.01 + t * 1.2 + wave) * 30;
                    ctx.LineTo(new Point(x, y), true, false);
                }
                ctx.LineTo(new Point(AreaWidth, AreaHeight), true, false);
            }
            geometry.Freeze();
            var brush = new SolidColorBrush(Color.FromArgb(18, 224, 178, 64));
            brush.Freeze();
            dc.DrawGeometry(brush, null, geometry);
        }
    }
}

public sealed class GradientAnimationBackground : AnimatedBackgroundBase
{
    protected override void RenderFrame(DrawingContext dc, double timeMs)
    {
        if (AreaWidth <= 0 || AreaHeight <= 0) return;
        var t = timeMs / 5000.0 * Speed;
        var c1 = ParseColor("#e0b240");
        var c2 = ParseColor("#12355b");
        var c3 = ParseColor("#f4e7c6");
        var brush = new LinearGradientBrush(c1, c2, 45 + t * 90);
        brush.GradientStops.Add(new GradientStop(c3, 0.5 + 0.2 * Math.Sin(t)));
        brush.Opacity = 0.18;
        brush.Freeze();
        dc.DrawRectangle(brush, null, new Rect(0, 0, AreaWidth, AreaHeight));
    }
}

public sealed class LinesBackground : AnimatedBackgroundBase
{
    protected override void RenderFrame(DrawingContext dc, double timeMs)
    {
        if (AreaWidth <= 0 || AreaHeight <= 0) return;
        var t = ScaledTimeSec(timeMs);
        var cx = AreaWidth / 2;
        var cy = AreaHeight / 2;
        for (var i = 0; i < 40; i++)
        {
            var angle = t * 0.15 + i * (Math.PI * 2 / 40);
            var len = Math.Max(AreaWidth, AreaHeight) * 0.6;
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(22, 224, 178, 64)), 1);
            pen.Freeze();
            dc.DrawLine(pen, new Point(cx, cy), new Point(cx + Math.Cos(angle) * len, cy + Math.Sin(angle) * len));
        }
    }
}

public sealed class SpotlightBackground : AnimatedBackgroundBase
{
    protected override void RenderFrame(DrawingContext dc, double timeMs)
    {
        if (AreaWidth <= 0 || AreaHeight <= 0) return;
        var t = ScaledTimeSec(timeMs);
        var x = AreaWidth * 0.5 + Math.Sin(t * 0.4) * AreaWidth * 0.25;
        var y = AreaHeight * 0.45 + Math.Cos(t * 0.35) * AreaHeight * 0.2;
        var brush = new RadialGradientBrush(Color.FromArgb(55, 224, 178, 64), Color.FromArgb(0, 0, 0, 0));
        brush.Freeze();
        dc.DrawEllipse(brush, null, new Point(x, y), 220, 220);
    }
}

public sealed class BeamsBackground : AnimatedBackgroundBase
{
    protected override void RenderFrame(DrawingContext dc, double timeMs)
    {
        if (AreaWidth <= 0 || AreaHeight <= 0) return;
        var t = ScaledTimeSec(timeMs);
        for (var i = 0; i < 6; i++)
        {
            var offset = (t * 40 + i * 120) % (AreaWidth + 200) - 100;
            var brush = new LinearGradientBrush(Color.FromArgb(0, 224, 178, 64), Color.FromArgb(35, 224, 178, 64), 90);
            brush.Freeze();
            var rect = new Rect(offset, -50, 60, AreaHeight + 100);
            dc.PushTransform(new RotateTransform(-18, offset + 30, AreaHeight / 2));
            dc.DrawRectangle(brush, null, rect);
            dc.Pop();
        }
    }
}
