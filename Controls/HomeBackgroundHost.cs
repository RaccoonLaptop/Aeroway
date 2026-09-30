using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZapretUI.Controls.Backgrounds;

namespace ZapretUI.Controls;

public sealed class HomeBackgroundHost : Grid
{
    private AnimatedBackgroundBase? _background;
    private bool _loopActive;
    private double _startMs;

    public string BackgroundId { get; private set; } = HomeBackgroundCatalog.All[0].Id;

    public AnimatedBackgroundBase? CurrentBackground => _background;

    public HomeBackgroundHost()
    {
        Loaded += (_, _) => StartLoop();
        Unloaded += (_, _) => StopLoop();
    }

    public void SetBackground(string? id, double? motionSpeed = null)
    {
        BackgroundId = HomeBackgroundCatalog.Normalize(id);
        if (motionSpeed is { } s)
            AnimatedBackgroundBase.GlobalSpeed = Math.Clamp(s, 0.1, 6.0);

        Children.Clear();
        _background = HomeBackgroundFactory.Create(BackgroundId);

        if (_background is null)
        {
            Children.Add(new Border
            {
                Background = (Brush)Application.Current.FindResource("BgBrush"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            });
            return;
        }

        _background.HorizontalAlignment = HorizontalAlignment.Stretch;
        _background.VerticalAlignment = VerticalAlignment.Stretch;
        Children.Add(_background);
        _startMs = 0;
    }

    public void ApplyMotionSpeed(double speed)
    {
        AnimatedBackgroundBase.GlobalSpeed = Math.Clamp(speed, 0.1, 6.0);
        _background?.InvalidateVisual();
    }

    private void StartLoop()
    {
        if (_loopActive) return;
        _loopActive = true;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopLoop()
    {
        if (!_loopActive) return;
        _loopActive = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_background is null || !IsVisible)
            return;
        if (Window.GetWindow(this) is { WindowState: WindowState.Minimized })
            return;

        var now = (DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;
        if (_startMs <= 0)
            _startMs = now;

        var timeMs = now - _startMs;
        _background.Step(timeMs);
        _background.InvalidateVisual();
    }
}
