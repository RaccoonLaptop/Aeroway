using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace ZapretUI.Helpers;

public static class Motion
{
    public const int Enter = 280;
    public const int Shift = 380;
    public const int CardSelect = 160;
    public const int ButtonPress = 80;
    public const int ButtonRelease = 160;
    public const int ToastIn = 180;
    public const int ToastHold = 1800;
    public const int ToastOut = 200;
    public const int PageChange = 200;
    public const int HoverCard = 120;

    private const uint SpiGetClientAreaAnimation = 0x1042;

    private static int _hooked;
    private static volatile bool _enabled = true;

    public static bool Enabled => _enabled;

    static Motion()
    {
        Refresh();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Interlocked.Exchange(ref _hooked, 1);
    }

    public static void Refresh()
    {
        var enabled = true;
        if (SystemParametersInfo(SpiGetClientAreaAnimation, 0, ref enabled, 0))
            _enabled = enabled;
    }

    public static void Stop()
    {
        if (Interlocked.Exchange(ref _hooked, 0) == 0)
            return;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    public static void To(UIElement target, DependencyProperty prop, double value, TimeSpan duration) =>
        To(target, prop, value, duration, null);

    public static void To(UIElement target, DependencyProperty prop, double value, TimeSpan duration, IEasingFunction? easing)
    {
        if (!_enabled)
        {
            target.BeginAnimation(prop, null);
            target.SetValue(prop, value);
            return;
        }

        target.BeginAnimation(prop, Animation(value, duration, easing), HandoffBehavior.SnapshotAndReplace);
    }

    public static void To(Animatable target, DependencyProperty prop, double value, TimeSpan duration) =>
        To(target, prop, value, duration, null);

    public static void To(Animatable target, DependencyProperty prop, double value, TimeSpan duration, IEasingFunction? easing)
    {
        if (!_enabled)
        {
            target.BeginAnimation(prop, null);
            target.SetValue(prop, value);
            return;
        }

        target.BeginAnimation(prop, Animation(value, duration, easing), HandoffBehavior.SnapshotAndReplace);
    }

    public static readonly DependencyProperty HoverScaleProperty =
        DependencyProperty.RegisterAttached(
            "HoverScale",
            typeof(double),
            typeof(Motion),
            new PropertyMetadata(0d, OnHoverScaleChanged));

    public static void SetHoverScale(DependencyObject element, double value) =>
        element.SetValue(HoverScaleProperty, value);

    public static double GetHoverScale(DependencyObject element) =>
        (double)element.GetValue(HoverScaleProperty);

    public static void Lift(FrameworkElement el)
    {
        var scale = new ScaleTransform(1, 1);
        var move = new TranslateTransform(0, 0);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(move);
        el.RenderTransformOrigin = new Point(0.5, 1);
        el.RenderTransform = group;

        el.MouseEnter += (_, _) =>
        {
            ScaleTo(scale, 1.028, 200, overshoot: true);
            Slide(move, -6, 220);
        };
        el.MouseLeave += (_, _) =>
        {
            ScaleTo(scale, 1, 200, overshoot: false);
            Slide(move, 0, 220);
        };
        el.PreviewMouseLeftButtonDown += (_, _) => ScaleTo(scale, 0.985, 80, overshoot: false);
        el.PreviewMouseLeftButtonUp += (_, _) =>
        {
            if (el.IsMouseOver)
            {
                ScaleTo(scale, 1.028, 160, overshoot: true);
                Slide(move, -6, 160);
            }
        };
    }

    public static void Reveal(FrameworkElement el)
    {
        var opacity = el.Opacity;
        el.BeginAnimation(UIElement.OpacityProperty, null);

        var move = el.RenderTransform as TranslateTransform;
        var y = 8d;
        if (move is not null)
        {
            y = move.Y;
            move.BeginAnimation(TranslateTransform.YProperty, null);
        }
        else
        {
            move = new TranslateTransform();
            el.RenderTransform = move;
            opacity = 0;
        }

        if (opacity >= 0.98)
        {
            opacity = 0;
            y = 8;
        }

        el.Opacity = opacity;
        move.Y = y;
        var duration = TimeSpan.FromMilliseconds(PageChange);
        To(el, UIElement.OpacityProperty, 1, duration);
        To(move, TranslateTransform.YProperty, 0, duration);
    }

    public static void Punch(ScaleTransform scale)
    {
        if (!_enabled)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
            return;
        }

        var x = new DoubleAnimation(1, 1.045, TimeSpan.FromMilliseconds(130))
        {
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        var y = x.Clone();
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, x, HandoffBehavior.SnapshotAndReplace);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, y, HandoffBehavior.SnapshotAndReplace);
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) =>
        Refresh();

    private static DoubleAnimation Animation(double value, TimeSpan duration, IEasingFunction? easing) =>
        new()
        {
            To = value,
            Duration = duration,
            EasingFunction = easing ?? new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };

    private static void OnHoverScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el || el.GetValue(BoundProperty) is true)
            return;
        var hover = (double)e.NewValue;
        if (hover <= 1)
            return;

        el.SetValue(BoundProperty, true);
        var scale = new ScaleTransform(1, 1);
        el.RenderTransformOrigin = new Point(0.5, 0.5);
        el.RenderTransform = scale;

        el.MouseEnter += (_, _) => ScaleTo(scale, hover, 180, overshoot: true);
        el.MouseLeave += (_, _) => ScaleTo(scale, 1, 200, overshoot: false);
        el.PreviewMouseLeftButtonDown += (_, _) => ScaleTo(scale, 0.96, 80, overshoot: false);
        el.PreviewMouseLeftButtonUp += (_, _) =>
            ScaleTo(scale, el.IsMouseOver ? hover : 1, 160, overshoot: el.IsMouseOver);
    }

    private static readonly DependencyProperty BoundProperty =
        DependencyProperty.RegisterAttached("Bound", typeof(bool), typeof(Motion), new PropertyMetadata(false));

    private static void ScaleTo(ScaleTransform scale, double to, int ms, bool overshoot)
    {
        var easing = overshoot
            ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 }
            : null;
        var duration = TimeSpan.FromMilliseconds(ms);
        To(scale, ScaleTransform.ScaleXProperty, to, duration, easing);
        To(scale, ScaleTransform.ScaleYProperty, to, duration, easing);
    }

    private static void Slide(TranslateTransform move, double to, int ms) =>
        To(move, TranslateTransform.YProperty, to, TimeSpan.FromMilliseconds(ms));

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);
}
