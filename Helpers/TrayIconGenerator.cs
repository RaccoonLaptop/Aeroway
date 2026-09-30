using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Bitmap = System.Drawing.Bitmap;
using DColor = System.Drawing.Color;
using Graphics = System.Drawing.Graphics;
using Icon = System.Drawing.Icon;
using LinearGradientBrush = System.Drawing.Drawing2D.LinearGradientBrush;
using Pen = System.Drawing.Pen;
using RectangleF = System.Drawing.RectangleF;
using SolidBrush = System.Drawing.SolidBrush;
using StringFormat = System.Drawing.StringFormat;

namespace ZapretUI.Helpers;

public static class TrayIconGenerator
{
    private static readonly DColor RingIdle = DColor.FromArgb(224, 178, 64);
    private static readonly DColor RingActive = DColor.FromArgb(143, 212, 96);
    private static readonly DColor ArcIdle = DColor.FromArgb(244, 231, 198);
    private static readonly DColor ArcActive = DColor.FromArgb(143, 212, 96);

    public enum Glyph
    {
        Stopped,
        Running,
        Fault
    }

    public static Icon Create(bool active) => Create(active ? Glyph.Running : Glyph.Stopped);

    public static Icon Create(Glyph glyph)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(DColor.Transparent);

            var scale = size / 512f;
            var pad = 8f * scale;
            var outer = new RectangleF(pad, pad, size - pad * 2, size - pad * 2);

            using (var grad = new LinearGradientBrush(
                       outer,
                       DColor.FromArgb(5, 11, 27),
                       DColor.FromArgb(18, 53, 91),
                       135f))
                g.FillEllipse(grad, outer);

            var fault = glyph == Glyph.Fault;
            var running = glyph == Glyph.Running;
            var ringColor = fault ? DColor.FromArgb(240, 112, 136) : running ? RingActive : RingIdle;
            var arcColor = fault ? DColor.FromArgb(240, 112, 136) : running ? ArcActive : ArcIdle;
            var ringWidth = Math.Max(1f, 10f * scale);
            var arcWidth = Math.Max(1f, 9f * scale);

            using (var ring = new Pen(ringColor, ringWidth))
                g.DrawEllipse(ring, 24f * scale, 24f * scale, size - 48f * scale, size - 48f * scale);

            using (var arcPen = new Pen(arcColor, arcWidth))
            {
                arcPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                arcPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawArc(arcPen, 72f * scale, 72f * scale, 368f * scale, 368f * scale, 210, 95);
            }

            using var textBrush = new SolidBrush(DColor.FromArgb(244, 231, 198));
            using var path = new GraphicsPath();
            using var family = new System.Drawing.FontFamily("Segoe UI");
            using var format = new StringFormat();
            path.AddString("A", family, (int)System.Drawing.FontStyle.Bold, 300f * scale, System.Drawing.PointF.Empty, format);
            var bounds = path.GetBounds();
            using (var matrix = new System.Drawing.Drawing2D.Matrix())
            {
                matrix.Translate(size / 2f - (bounds.X + bounds.Width / 2f), size / 2f - (bounds.Y + bounds.Height / 2f));
                path.Transform(matrix);
            }
            g.FillPath(textBrush, path);
        }

        var handle = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}
