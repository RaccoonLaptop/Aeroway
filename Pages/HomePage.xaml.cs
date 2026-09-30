using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ZapretUI.Controls.Backgrounds;
using ZapretUI.Helpers;
using ZapretUI.Services;

namespace ZapretUI.Pages;

public partial class HomePage : UserControl
{
    private readonly ZapretPaths _paths;
    private readonly StrategyService _strategy;
    private readonly AppSettings _settings;
    private ComboBox _strategyCombo = null!;
    private const double CardWidth = 156;
    private const double CardGap = 12;
    private const double CardStride = CardWidth + CardGap;
    private const int VisibleCards = 3;

    public ObservableCollection<StrategyCard> Cards { get; } = new();
    private TranslateTransform _stripShift = null!;
    private bool _stripPlaced;
    private Panel _dots = null!;
    private Border _ring = null!;
    private ScaleTransform _ringScale = null!;
    private int _ringMode = -1;
    private string? _launchCaption;
    private ToggleButton _autostartToggle = null!;
    private Button _toggleBtn = null!;
    private ScaleTransform _launchScale = null!;
    private string? _launchStyle;
    private TextBlock _healthBanner = null!;
    private Button _healthGuideBtn = null!;
    private readonly DispatcherTimer _statusTimer;
    private bool _isStarting;
    private bool _suppressComboChange;
    private CancellationTokenSource? _startCts;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public bool IsBypassBusy => _isStarting;

    public HomePage(ZapretPaths paths, StrategyService strategy, AppSettings settings)
    {
        _paths = paths;
        _strategy = strategy;
        _settings = settings;
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) => RefreshToggleUi();
        Unloaded += (_, _) => _statusTimer.Stop();
        InitializeComponent();
        BuildUi();
        _statusTimer.Start();
        RefreshToggleUi();
    }

    public string? GetSelectedStrategy() => GetSelectedFileName();

    public void RememberAppliedStrategy(string fileName)
    {
        TrySelectStrategy(fileName);
        RefreshToggleUi();
    }

    public async Task SwitchStrategyAsync(string strategy)
    {
        await _operationGate.WaitAsync();
        _suppressComboChange = true;
        try
        {
            if (!TrySelectStrategy(strategy))
                return;

            _settings.LastStrategy = strategy;
            _settings.Save();

            if (!_strategy.IsRunning() && !_isStarting)
                return;

            _startCts?.Cancel();
            _startCts?.Dispose();
            _startCts = new CancellationTokenSource();
            var ct = _startCts.Token;

            try
            {
                _isStarting = true;
                Toast(Loc.T("home.prep"));
                RefreshToggleUi();

                await _strategy.StopStrategyAsync(ct);
                Toast(Loc.T("home.wait_winws"));
                ConsoleLog.Instance.Write(Loc.F("home.log_start", strategy));
                await _strategy.StartStrategyAsync(strategy, ct, quickSwitch: true);
                ConsoleLog.Instance.Write(Loc.T("home.log_started"));
                Toast(Loc.T("home.done"));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ConsoleLog.Instance.Write($"{Loc.T("common.error_prefix")} {ex.Message}");
                UiHelpers.ShowError(ex.Message);
            }
            finally
            {
                _startCts?.Dispose();
                _startCts = null;
                _isStarting = false;
                RefreshToggleUi();
            }
        }
        finally
        {
            _suppressComboChange = false;
            _operationGate.Release();
        }
    }

    private void BuildUi()
    {
        AboveStrip.Children.Add(new TextBlock
        {
            Text = "Aeroway",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)Application.Current.FindResource("TextBrush"),
            Margin = new Thickness(0, 0, 0, 16)
        });

        _healthBanner = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Foreground = (Brush)Application.Current.FindResource("ErrorBrush"),
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = Visibility.Collapsed
        };
        AboveStrip.Children.Add(_healthBanner);
        _healthGuideBtn = new Button
        {
            Content = Loc.T("health.open_guide"),
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12),
            Visibility = Visibility.Collapsed
        };
        _healthGuideBtn.Click += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ZapretHealth.GuideUrl) { UseShellExecute = true });
        AboveStrip.Children.Add(_healthGuideBtn);

        _strategyCombo = new ComboBox
        {
            Visibility = Visibility.Collapsed,
            DisplayMemberPath = nameof(StrategyItem.DisplayName),
            SelectedValuePath = nameof(StrategyItem.FileName)
        };
        foreach (var item in StrategyDisplayHelper.LoadItems(_paths.Root, _paths.GetStrategyFiles()))
        {
            _strategyCombo.Items.Add(item);
            Cards.Add(new StrategyCard { FileName = item.FileName, Title = item.DisplayName });
        }
        _strategyCombo.SelectionChanged += async (_, _) =>
        {
            await OnStrategySelectionChangedAsync();
            AlignStrip();
        };
        AboveStrip.Children.Add(_strategyCombo);

        _stripShift = new TranslateTransform();
        StrategyStrip.RenderTransform = _stripShift;
        StrategyStrip.ItemsSource = Cards;
        var shown = Math.Min(VisibleCards, Math.Max(1, Cards.Count));
        StripViewport.Width = CardStride * shown;

        _dots = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 460,
            Margin = new Thickness(0, 14, 0, 0)
        };
        BelowStrip.Children.Add(_dots);

        SelectDefaultStrategy();

        var launchRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 18, 0, 0)
        };
        launchRow.Children.Add(ArrowButton("‹", -1));

        _ringScale = new ScaleTransform(1, 1);
        _ring = new Border
        {
            Width = 250,
            Height = 58,
            CornerRadius = new CornerRadius(29),
            BorderThickness = new Thickness(1.4),
            BorderBrush = (Brush)Application.Current.FindResource("AccentBrush"),
            Background = Brushes.Transparent,
            Opacity = 0.35,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _ringScale
        };
        _toggleBtn = new Button
        {
            Content = Loc.T("home.start"),
            Style = (Style)Application.Current.FindResource("PillButton"),
            MinWidth = 220,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _launchCaption = Loc.T("home.start");
        _toggleBtn.SizeChanged += (_, _) =>
        {
            _ring.Width = _toggleBtn.ActualWidth + 22;
            _ring.Height = _toggleBtn.ActualHeight + 14;
            _ring.CornerRadius = new CornerRadius(_ring.Height / 2);
        };
        _toggleBtn.Click += async (_, _) => await ToggleAsync();
        _launchScale = new ScaleTransform(1, 1);
        _toggleBtn.RenderTransformOrigin = new Point(0.5, 0.5);
        _toggleBtn.RenderTransform = _launchScale;
        _toggleBtn.MouseEnter += (_, _) => LaunchScale(1.03, Motion.ButtonPress, null);
        _toggleBtn.MouseLeave += (_, _) =>
        {
            if (_toggleBtn.IsPressed)
                return;
            LaunchScale(1, Motion.ButtonRelease, null);
        };
        _toggleBtn.PreviewMouseLeftButtonDown += (_, _) => LaunchScale(0.97, Motion.ButtonPress, null);
        _toggleBtn.PreviewMouseLeftButtonUp += (_, _) =>
            LaunchScale(1, Motion.ButtonRelease, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 });

        var stage = new Grid { Margin = new Thickness(16, 0, 16, 0) };
        stage.Children.Add(_ring);
        stage.Children.Add(_toggleBtn);
        launchRow.Children.Add(stage);
        launchRow.Children.Add(ArrowButton("›", 1));
        BelowStrip.Children.Add(launchRow);
        SyncRing(0);

        BelowStrip.Children.Add(CreateAutostartRow());
        AlignStrip();
    }

    private Button ArrowButton(string glyph, int step)
    {
        var button = new Button
        {
            Content = glyph,
            Style = (Style)Application.Current.FindResource("IconButton"),
            FontSize = 20,
            ToolTip = step < 0 ? "Предыдущая" : "Следующая"
        };
        button.Click += (_, _) => StepStrategy(step);
        return button;
    }

    private void StepStrategy(int step)
    {
        if (_strategyCombo.Items.Count == 0 || _isStarting)
            return;
        var next = _strategyCombo.SelectedIndex + step;
        if (next < 0)
            next = _strategyCombo.Items.Count - 1;
        if (next >= _strategyCombo.Items.Count)
            next = 0;
        _strategyCombo.SelectedIndex = next;
    }

    private void StripViewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        StepStrategy(e.Delta > 0 ? -1 : 1);
        e.Handled = true;
    }

    private void Card_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isStarting || sender is not Border card || card.DataContext is not StrategyCard model)
            return;
        var index = Cards.IndexOf(model);
        if (index >= 0)
            _strategyCombo.SelectedIndex = index;
    }

    private void Card_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Border card || card.DataContext is not StrategyCard { IsSelected: false })
            return;
        if (HoverEdge(card) is { } edge)
            Motion.To(edge, OpacityProperty, 1, TimeSpan.FromMilliseconds(Motion.HoverCard));
    }

    private void Card_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Border card || HoverEdge(card) is not { } edge)
            return;
        Motion.To(edge, OpacityProperty, 0, TimeSpan.FromMilliseconds(Motion.HoverCard));
    }

    private static Border? HoverEdge(Border card)
    {
        if (card.Child is not Grid grid || grid.Children.Count == 0)
            return null;
        return grid.Children[0] as Border;
    }

    private void AlignStrip()
    {
        if (_stripShift is null || _strategyCombo is null || _dots is null)
            return;

        var count = Cards.Count;
        var selected = count == 0 ? 0 : Math.Max(0, _strategyCombo.SelectedIndex);
        for (var i = 0; i < count; i++)
            Cards[i].IsSelected = i == selected;

        RenderDots(selected);
        var target = -WindowStart(selected) * CardStride;
        var current = _stripShift.X;
        if (!_stripPlaced || Math.Abs(current - target) < 0.5)
        {
            _stripShift.BeginAnimation(TranslateTransform.XProperty, null);
            _stripShift.X = target;
            _stripPlaced = true;
            return;
        }

        current = _stripShift.X;
        _stripShift.BeginAnimation(TranslateTransform.XProperty, null);
        _stripShift.X = current;
        Motion.To(_stripShift, TranslateTransform.XProperty, target, TimeSpan.FromMilliseconds(Motion.Shift));
    }

    private int WindowStart(int selected)
    {
        var count = Cards.Count;
        var visible = Math.Min(VisibleCards, count);
        if (count <= visible)
            return 0;
        var start = selected - 1;
        if (start < 0)
            start = 0;
        if (start > count - visible)
            start = count - visible;
        return start;
    }

    private void RenderDots(int selected)
    {
        _dots.Children.Clear();
        var count = _strategyCombo.Items.Count;
        if (count <= VisibleCards)
            return;
        for (var i = 0; i < count; i++)
            _dots.Children.Add(MakeDot(i, i == selected));
    }

    private Border MakeDot(int index, bool selected)
    {
        var dot = new Border
        {
            Width = selected ? 16 : 6,
            Height = 6,
            Margin = new Thickness(3, 0, 3, 0),
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(selected
                ? Color.FromRgb(224, 178, 64)
                : Color.FromArgb(120, 201, 184, 150)),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var captured = index;
        dot.MouseLeftButtonUp += (_, _) => _strategyCombo.SelectedIndex = captured;
        return dot;
    }

    private void LaunchScale(double to, int ms, IEasingFunction? easing)
    {
        var duration = TimeSpan.FromMilliseconds(ms);
        Motion.To(_launchScale, ScaleTransform.ScaleXProperty, to, duration, easing);
        Motion.To(_launchScale, ScaleTransform.ScaleYProperty, to, duration, easing);
    }

    private void SyncRing(int mode)
    {
        if (mode == _ringMode)
            return;
        _ringMode = mode;
        _ringScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _ringScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _ringScale.ScaleX = 1;
        _ringScale.ScaleY = 1;
        _ring.BorderThickness = new Thickness(1.4);
        var key = mode switch
        {
            2 => "SuccessBrush",
            1 => "TextMutedBrush",
            _ => "AccentFillBrush"
        };
        _ring.BorderBrush = (Brush)Application.Current.FindResource(key);
        _ring.Opacity = mode switch
        {
            2 => 0.9,
            1 => 0.35,
            _ => 0.55
        };
    }

    private void Toast(string text, bool error = false)
    {
        if (Window.GetWindow(this) is MainWindow window)
            window.ShowToast(text, error);
    }

    private UIElement CreateAutostartRow()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 22, 0, 0)
        };

        row.Children.Add(new TextBlock
        {
            Text = Loc.T("home.autostart"),
            FontSize = 13,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        });

        _autostartToggle = new ToggleButton
        {
            Style = (Style)Application.Current.FindResource("SwitchToggle"),
            IsChecked = _settings.StartUiOnLogin && AppStartupService.IsEnabled(),
            VerticalAlignment = VerticalAlignment.Center
        };
        _autostartToggle.Checked += (_, _) => SetAutostartEnabled(true);
        _autostartToggle.Unchecked += (_, _) => SetAutostartEnabled(false);
        row.Children.Add(_autostartToggle);

        return row;
    }

    private void SetAutostartEnabled(bool enabled)
    {
        if (enabled)
        {
            var strategy = GetSelectedFileName();
            if (!string.IsNullOrEmpty(strategy))
                _settings.LastStrategy = strategy;
            AppStartupService.Enable();
            _settings.StartUiOnLogin = true;
        }
        else
        {
            AppStartupService.Disable();
            _settings.StartUiOnLogin = false;
        }
        _settings.Save();
    }

    private void SelectDefaultStrategy()
    {
        _suppressComboChange = true;
        try
        {
            if (TrySelectStrategy(_settings.LastStrategy))
                return;

            var running = _strategy.GetRunningStrategyTitle();
            if (!string.IsNullOrEmpty(running))
            {
                var runningBat = running.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                    ? running
                    : running + ".bat";
                if (TrySelectStrategy(runningBat))
                    return;
            }

            foreach (var name in new[] { "general.bat", "general (SIMPLE FAKE).bat" })
            {
                if (TrySelectStrategy(name))
                    return;
            }

            if (_strategyCombo.Items.Count > 0)
                _strategyCombo.SelectedIndex = 0;
        }
        finally
        {
            _suppressComboChange = false;
        }
    }

    private bool TrySelectStrategy(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        foreach (StrategyItem item in _strategyCombo.Items)
        {
            if (!item.FileName.Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (ReferenceEquals(_strategyCombo.SelectedItem, item))
                return true;

            if (_suppressComboChange)
            {
                _strategyCombo.SelectedItem = item;
                return true;
            }

            _suppressComboChange = true;
            try
            {
                _strategyCombo.SelectedItem = item;
            }
            finally
            {
                _suppressComboChange = false;
            }

            return true;
        }

        return false;
    }

    private StrategyItem? GetSelectedStrategyItem() =>
        _strategyCombo.SelectedItem as StrategyItem;

    private string? GetSelectedFileName() =>
        GetSelectedStrategyItem()?.FileName ?? _settings.LastStrategy;

    private async Task OnStrategySelectionChangedAsync()
    {
        if (_suppressComboChange) return;

        try
        {
            if (GetSelectedFileName() is not { } strategy) return;
            if (_isStarting) return;


            if (_strategy.IsRunning())
                await SwitchStrategyAsync(strategy);
            else
            {
                _settings.LastStrategy = strategy;
                _settings.Save();
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Instance.Write($"{Loc.T("common.error_prefix")} {ex.Message}");
            UiHelpers.ShowError(ex.Message);
        }
    }

    public void RefreshToggleUi()
    {
        if (_toggleBtn is null || _strategyCombo is null)
            return;

        var running = _strategy.IsRunning();
        var caption = _isStarting && !running
            ? Loc.T("home.starting")
            : (running || _isStarting ? Loc.T("home.stop") : Loc.T("home.start"));
        if (_launchCaption != caption)
        {
            _toggleBtn.Content = caption;
            _launchCaption = caption;
        }

        _toggleBtn.IsEnabled = !_isStarting;
        _strategyCombo.IsEnabled = !_isStarting;

        var styleKey = _isStarting ? "PillBusyButton" : running ? "PillStopButton" : "PillButton";
        if (_launchStyle != styleKey)
        {
            _toggleBtn.Style = (Style)Application.Current.FindResource(styleKey);
            _toggleBtn.RenderTransform = _launchScale;
            _toggleBtn.RenderTransformOrigin = new Point(0.5, 0.5);
            _launchStyle = styleKey;
        }

        SyncRing(_isStarting ? 1 : running ? 2 : 0);

        var missing = ZapretHealth.MissingFiles(_paths);
        var filesMissing = missing.Count > 0;
        _healthBanner.Text = filesMissing ? Loc.F("strategy.files_missing", string.Join(", ", missing)) : "";
        _healthBanner.Visibility = filesMissing ? Visibility.Visible : Visibility.Collapsed;
        _healthGuideBtn.Visibility = filesMissing ? Visibility.Visible : Visibility.Collapsed;
    }

    public Task ToggleBypassAsync() => ToggleAsync();

    private async Task ToggleAsync()
    {
        await _operationGate.WaitAsync();
        try
        {
            if (_strategy.IsRunning() || _isStarting)
            {
                _startCts?.Cancel();
                await _strategy.StopStrategyAsync();
                _isStarting = false;
                ConsoleLog.Instance.Write(Loc.T("home.log_stopped"));
                RefreshToggleUi();
                return;
            }

            if (GetSelectedFileName() is not { } strategy)
            {
                UiHelpers.ShowError(Loc.T("home.select_strategy"));
                return;
            }

            _startCts?.Cancel();
            _startCts?.Dispose();
            _startCts = new CancellationTokenSource();
            var ct = _startCts.Token;

            try
            {
                _isStarting = true;
                Toast(Loc.T("home.prep"));
                RefreshToggleUi();

                _settings.LastStrategy = strategy;
                _settings.Save();
                ConsoleLog.Instance.Write(Loc.F("home.log_start", strategy));

                Toast(Loc.T("home.wait_winws"));
                await _strategy.StartStrategyAsync(strategy, ct);

                ConsoleLog.Instance.Write(Loc.T("home.log_started"));
                Toast(Loc.T("home.done"));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ConsoleLog.Instance.Write($"{Loc.T("common.error_prefix")} {ex.Message}");
                UiHelpers.ShowError(ex.Message);
            }
            finally
            {
                _startCts?.Dispose();
                _startCts = null;
                _isStarting = false;
                RefreshToggleUi();
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }
}
