using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using System.Windows.Threading;
using ZapretUI.Helpers;
using ZapretUI.Models;
using ZapretUI.Services;

namespace ZapretUI;

public sealed class PresetTestRunPanel : UserControl
{
    private readonly ZapretPaths _paths;
    private readonly StrategyService _strategy;
    private readonly AppSettings _settings;
    private readonly IReadOnlyList<string> _batFiles;
    private readonly EmbeddedTerminalHost _terminal = new();
    private readonly AnsiTerminalRenderer _ansi = new();
    private readonly Dictionary<PresetTestKind, TestRunTracker> _trackers = new()
    {
        [PresetTestKind.Standard] = new TestRunTracker(),
        [PresetTestKind.DpiFreeze] = new TestRunTracker()
    };
    private readonly Dictionary<PresetTestKind, KindViewState> _viewStates = new()
    {
        [PresetTestKind.Standard] = new KindViewState(),
        [PresetTestKind.DpiFreeze] = new KindViewState()
    };
    private PresetTestKind _displayKind = PresetTestKind.Standard;
    private PresetTestKind _runKind = PresetTestKind.Standard;
    private TestScriptAutoResponder? _autoResponder;
    private PresetTestScope _scope = new();

    private RichTextBox _output = null!;
    private TextBox _input = null!;
    private Button _applyBestBtn = null!;
    private TextBlock _statusText = null!;
    private TextBlock _progressText = null!;
    private ProgressBar _progressBar = null!;
    private TextBlock _currentPresetText = null!;
    private StackPanel _targetsPanel = null!;
    private StackPanel _scoresPanel = null!;
    private ScrollViewer _targetsScroll = null!;
    private ScrollViewer _scoresScroll = null!;
    private Border _logPanel = null!;
    private bool _logVisible;
    private DispatcherTimer? _transitionTimer;
    private DispatcherTimer? _saveTimer;
    private bool _terminalHandlersAttached;

    private TestRunTracker DisplayTracker => _trackers[_displayKind];
    private KindViewState DisplayView => _viewStates[_displayKind];
    private TestRunTracker RunTracker => _trackers[_runKind];

    private sealed class KindViewState
    {
        public string LastSeenPreset = "";
        public string? ReviewPresetFile;
        public bool InTransition;
        public PresetTargetSnapshot? TransitionSnapshot;
    }

    public PresetTestRunPanel(ZapretPaths paths, StrategyService strategy, AppSettings settings)
    {
        _paths = paths;
        _strategy = strategy;
        _settings = settings;
        _batFiles = paths.GetStrategyFiles().ToList();

        VerticalAlignment = VerticalAlignment.Stretch;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        BuildUi();
        foreach (var pair in _trackers)
            pair.Value.Changed += () => OnTrackerChanged(pair.Key);
        Loaded += OnPanelLoaded;
        Unloaded += OnPanelUnloaded;
    }

    private void OnPanelLoaded(object? sender, RoutedEventArgs e) => AttachTerminalHandlers();

    private async void OnPanelUnloaded(object? sender, RoutedEventArgs e)
    {
        _transitionTimer?.Stop();
        _saveTimer?.Stop();

        if (_terminal.IsRunning || IsRunning)
            await StopTestAsync();
        else
            SaveSession();

        DetachTerminalHandlers();
    }

    public async Task DisposeTerminalAsync()
    {
        _transitionTimer?.Stop();
        _saveTimer?.Stop();
        await StopTestAsync();
        DetachTerminalHandlers();
        await _terminal.DisposeAsync();
    }

    private void AttachTerminalHandlers()
    {
        if (_terminalHandlersAttached) return;
        _terminal.OutputReceived += OnTerminalOutput;
        _terminal.ProcessExited += OnTerminalExited;
        _terminal.ErrorOccurred += OnTerminalError;
        _terminalHandlersAttached = true;
    }

    private void DetachTerminalHandlers()
    {
        if (!_terminalHandlersAttached) return;
        _terminal.OutputReceived -= OnTerminalOutput;
        _terminal.ProcessExited -= OnTerminalExited;
        _terminal.ErrorOccurred -= OnTerminalError;
        _terminalHandlersAttached = false;
    }

    public PresetTestKind RestoreFromStorage()
    {
        var session = TestRunStateStore.Load();
        _trackers[PresetTestKind.Standard].ImportKindState(session.Standard);
        _trackers[PresetTestKind.DpiFreeze].ImportKindState(session.Dpi);
        _viewStates[PresetTestKind.Standard].ReviewPresetFile = session.Standard.ReviewPresetFile;
        _viewStates[PresetTestKind.DpiFreeze].ReviewPresetFile = session.Dpi.ReviewPresetFile;

        _displayKind = session.LastSelectedKind == nameof(PresetTestKind.DpiFreeze)
            ? PresetTestKind.DpiFreeze
            : PresetTestKind.Standard;

        RefreshVisualState();
        return _displayKind;
    }

    public void SaveSession()
    {
        var session = new SavedTestRunSession
        {
            LastSelectedKind = _displayKind.ToString(),
            Standard = _trackers[PresetTestKind.Standard].ExportKindState(
                _viewStates[PresetTestKind.Standard].ReviewPresetFile),
            Dpi = _trackers[PresetTestKind.DpiFreeze].ExportKindState(
                _viewStates[PresetTestKind.DpiFreeze].ReviewPresetFile)
        };
        TestRunStateStore.Save(session);
    }

    private void SchedulePersist()
    {
        _saveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Stop();
        _saveTimer.Tick -= OnSaveTimerTick;
        _saveTimer.Tick += OnSaveTimerTick;
        _saveTimer.Start();
    }

    private void OnSaveTimerTick(object? sender, EventArgs e)
    {
        _saveTimer?.Stop();
        SaveSession();
    }

    public async Task StartAsync(PresetTestKind kind, PresetTestScope scope)
    {
        _displayKind = kind;
        _runKind = kind;
        _scope = scope;

        if (_terminal.IsRunning)
            await _terminal.StopAsync();

        await StartTestAsync();
    }

    public void SwitchDisplayKind(PresetTestKind kind)
    {
        if (IsRunning || _displayKind == kind) return;

        ResetTransitionState(_displayKind);
        _displayKind = kind;
        RefreshVisualState();
        SchedulePersist();
    }

    public bool HasVisibleContent =>
        _trackers.Values.Any(t => t.Scores.Count > 0 || t.Targets.Count > 0);

    public bool IsRunning => _trackers.Values.Any(t => t.IsRunning);

    public async Task StopAsync()
    {
        await StopTestAsync();
    }

    public event Action? RunStateChanged;

    private void BuildUi()
    {
        var root = new Grid { Margin = new Thickness(0, 20, 0, 0) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 0 });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var progressCard = CreateCard();
        progressCard.Margin = new Thickness(0, 0, 0, 12);
        var progressStack = new StackPanel();
        var progressHeader = new Grid();
        progressHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        progressHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _statusText = new TextBlock
        {
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 12, 0)
        };
        _progressText = new TextBlock
        {
            Foreground = (Brush)Application.Current.FindResource("AccentBrush"),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_statusText, 0);
        Grid.SetColumn(_progressText, 1);
        progressHeader.Children.Add(_statusText);
        progressHeader.Children.Add(_progressText);
        progressStack.Children.Add(progressHeader);
        _progressBar = new ProgressBar
        {
            Height = 6,
            Minimum = 0,
            Maximum = 1,
            Margin = new Thickness(0, 8, 0, 0),
            Background = (Brush)Application.Current.FindResource("InputBrush"),
            Foreground = (Brush)Application.Current.FindResource("AccentBrush"),
            BorderThickness = new Thickness(0)
        };
        progressStack.Children.Add(_progressBar);
        progressCard.Child = progressStack;
        Grid.SetRow(progressCard, 0);
        root.Children.Add(progressCard);

        var split = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star), MinWidth = 360 });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 280 });

        var leftCard = CreateCard();
        leftCard.Padding = new Thickness(14);
        leftCard.VerticalAlignment = VerticalAlignment.Stretch;
        leftCard.ClipToBounds = true;
        var leftGrid = new Grid { VerticalAlignment = VerticalAlignment.Stretch };
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 0 });
        var targetsTitle = MakeSectionTitle(Loc.T("tools.test_targets_title"));
        Grid.SetRow(targetsTitle, 0);
        leftGrid.Children.Add(targetsTitle);
        _currentPresetText = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            FontSize = 13.5,
            Margin = new Thickness(0, 4, 0, 10),
            Foreground = (Brush)Application.Current.FindResource("AccentBrush"),
            TextWrapping = TextWrapping.Wrap,
            Text = Loc.T("tools.test_current_strategy_waiting")
        };
        Grid.SetRow(_currentPresetText, 1);
        leftGrid.Children.Add(_currentPresetText);
        _targetsScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = true,
            CanContentScroll = false
        };
        _targetsPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _targetsScroll.Content = _targetsPanel;
        MouseWheelScrollHelper.Attach(_targetsScroll);
        Grid.SetRow(_targetsScroll, 2);
        leftGrid.Children.Add(_targetsScroll);
        leftCard.Child = leftGrid;
        Grid.SetColumn(leftCard, 0);
        split.Children.Add(leftCard);

        var rightCard = CreateCard();
        rightCard.Padding = new Thickness(14);
        rightCard.VerticalAlignment = VerticalAlignment.Stretch;
        var rightGrid = new Grid { VerticalAlignment = VerticalAlignment.Stretch };
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 0 });
        var scoresTitle = MakeSectionTitle(Loc.T("tools.test_scores_title"));
        Grid.SetRow(scoresTitle, 0);
        rightGrid.Children.Add(scoresTitle);
        var scoresHint = new TextBlock
        {
            Text = Loc.T("tools.test_scores_hint"),
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(scoresHint, 1);
        rightGrid.Children.Add(scoresHint);
        _scoresScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = true,
            CanContentScroll = false
        };
        _scoresPanel = new StackPanel();
        _scoresScroll.Content = _scoresPanel;
        MouseWheelScrollHelper.Attach(_scoresScroll);
        Grid.SetRow(_scoresScroll, 2);
        rightGrid.Children.Add(_scoresScroll);
        rightCard.Child = rightGrid;
        Grid.SetColumn(rightCard, 2);
        split.Children.Add(rightCard);

        Grid.SetRow(split, 1);
        root.Children.Add(split);

        var footer = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
        _applyBestBtn = MakeButton(Loc.T("tools.test_apply_best"), async () => await ApplyBestAsync());
        _applyBestBtn.IsEnabled = false;
        footer.Children.Add(_applyBestBtn);
        footer.Children.Add(MakeButton(Loc.T("tools.test_toggle_log"), () =>
        {
            _logVisible = !_logVisible;
            _logPanel.Visibility = _logVisible ? Visibility.Visible : Visibility.Collapsed;
            return Task.CompletedTask;
        }));
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        _logPanel = CreateLogPanel();
        _logPanel.Visibility = Visibility.Collapsed;
        _logPanel.Margin = new Thickness(0, 10, 0, 0);
        Grid.SetRow(_logPanel, 3);
        root.Children.Add(_logPanel);

        Content = root;
        RefreshVisualState();
    }

    private Border CreateLogPanel()
    {
        _output = new RichTextBox
        {
            IsReadOnly = true,
            Height = 160,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = TerminalFonts.Mono,
            FontSize = 12,
            Background = (Brush)Application.Current.FindResource("InputBrush"),
            BorderThickness = new Thickness(0),
            Document = new FlowDocument()
        };
        TerminalFonts.ApplyDisplayMode(_output);
        AnsiTerminalRenderer.ApplyTerminalLayout(_output.Document);
        MouseWheelScrollHelper.Attach(_output);

        _input = new TextBox
        {
            FontFamily = TerminalFonts.Mono,
            FontSize = 12,
            IsEnabled = false,
            Margin = new Thickness(0, 6, 0, 0)
        };
        MouseWheelScrollHelper.Attach(_input);
        _input.KeyDown += async (_, e) =>
        {
            if (!_terminal.IsRunning || e.Key != Key.Enter) return;
            e.Handled = true;
            var line = _input.Text;
            _input.Clear();
            await _terminal.WriteInputAsync(string.IsNullOrEmpty(line) ? "" : line);
        };

        var stack = new StackPanel();
        stack.Children.Add(_output);
        stack.Children.Add(_input);
        var border = CreateCard();
        border.Child = stack;
        return border;
    }

    private void RefreshVisualState()
    {
        var tracker = DisplayTracker;
        var view = DisplayView;

        _statusText.Text = tracker.StatusText;
        _progressText.Text = tracker.ProgressText;
        _progressBar.Value = tracker.Progress;

        var (targets, presetDisplay) = ResolveDisplayTargets(tracker, view);
        _currentPresetText.Text = string.IsNullOrWhiteSpace(presetDisplay)
            ? Loc.T("tools.test_current_strategy_waiting")
            : Loc.F("tools.test_current_strategy", presetDisplay);

        _applyBestBtn.IsEnabled = !string.IsNullOrWhiteSpace(tracker.BestPresetFile) && !IsRunning;

        _targetsPanel.Children.Clear();
        if (targets.Count == 0)
        {
            _targetsPanel.Children.Add(MakeMuted(Loc.T("tools.test_targets_empty")));
        }
        else
        {
            TestTargetTableRenderer.Render(_targetsPanel, targets);
        }

        _scoresPanel.Children.Clear();
        if (tracker.Scores.Count == 0)
            _scoresPanel.Children.Add(MakeMuted(Loc.T("tools.test_scores_empty")));
        else
            foreach (var score in tracker.Scores)
                _scoresPanel.Children.Add(BuildScoreCard(score, tracker, view));
    }

    private (IReadOnlyList<TestTargetRow> Targets, string PresetDisplay) ResolveDisplayTargets(
        TestRunTracker tracker,
        KindViewState view)
    {
        if (!tracker.IsRunning && view.ReviewPresetFile is not null
            && tracker.TryGetSnapshot(view.ReviewPresetFile, out var review))
            return (review.Targets, review.DisplayName);

        if (view.InTransition && view.TransitionSnapshot is not null)
            return (view.TransitionSnapshot.Targets, view.TransitionSnapshot.DisplayName);

        return (tracker.Targets, tracker.CurrentPresetDisplay);
    }

    private void HandlePresetTransition(TestRunTracker tracker, KindViewState view)
    {
        var current = tracker.CurrentPreset;
        if (tracker.IsRunning
            && !string.IsNullOrEmpty(view.LastSeenPreset)
            && !string.IsNullOrEmpty(current)
            && !current.Equals(view.LastSeenPreset, StringComparison.OrdinalIgnoreCase)
            && tracker.TryGetSnapshot(view.LastSeenPreset, out var snapshot))
        {
            view.InTransition = true;
            view.TransitionSnapshot = snapshot;
            view.ReviewPresetFile = null;

            _transitionTimer?.Stop();
            _transitionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _transitionTimer.Tick += (_, _) =>
            {
                _transitionTimer?.Stop();
                view.InTransition = false;
                view.TransitionSnapshot = null;
                RefreshVisualState();
            };
            _transitionTimer.Start();
        }

        if (!string.IsNullOrEmpty(current))
            view.LastSeenPreset = current;
    }

    private void ResetTransitionState(PresetTestKind kind)
    {
        var view = _viewStates[kind];
        view.LastSeenPreset = "";
        view.ReviewPresetFile = null;
        view.InTransition = false;
        view.TransitionSnapshot = null;
    }

    private Border BuildScoreCard(PresetScoreRow score, TestRunTracker tracker, KindViewState view)
    {
        var isSelected = !tracker.IsRunning
            && view.ReviewPresetFile is not null
            && score.FileName.Equals(view.ReviewPresetFile, StringComparison.OrdinalIgnoreCase);

        var card = new Border
        {
            Background = (Brush)Application.Current.FindResource(isSelected ? "PanelOverlayBrush" : "InputBrush"),
            BorderBrush = (Brush)Application.Current.FindResource(isSelected ? "AccentBrush" : "BorderBrush"),
            BorderThickness = new Thickness(isSelected ? 2 : 1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Cursor = tracker.IsRunning ? Cursors.Arrow : Cursors.Hand
        };
        card.MouseLeftButtonUp += (_, e) =>
        {
            if (tracker.IsRunning) return;
            if (e.OriginalSource is Button) return;
            view.ReviewPresetFile = score.FileName;
            RefreshVisualState();
            SchedulePersist();
        };
        var stack = new StackPanel();

        var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 6) };
        var applyBtn = new Button
        {
            Content = "▶",
            Padding = new Thickness(10, 4, 10, 4),
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            ToolTip = Loc.T("tools.test_apply_preset"),
            VerticalAlignment = VerticalAlignment.Top
        };
        applyBtn.Click += async (_, _) => await ApplyPresetAsync(score.FileName);
        DockPanel.SetDock(applyBtn, Dock.Right);

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 0) };
        titleRow.Children.Add(new TextBlock
        {
            Text = score.Glyph,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Width = 18,
            Margin = new Thickness(0, 0, 6, 0),
            Foreground = score.Glyph switch
            {
                "✓" => (Brush)Application.Current.FindResource("SuccessBrush"),
                "≈" => (Brush)Application.Current.FindResource("WarningBrush"),
                _ => (Brush)Application.Current.FindResource("ErrorBrush")
            }
        });
        titleRow.Children.Add(new TextBlock
        {
            Text = score.DisplayName,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });

        header.Children.Add(applyBtn);
        header.Children.Add(titleRow);
        stack.Children.Add(header);
        stack.Children.Add(new TextBlock
        {
            Text = score.Detail,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            FontSize = 11,
            Margin = new Thickness(24, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None
        });
        card.Child = stack;
        return card;
    }

    private async Task StartTestAsync()
    {
        var owner = Window.GetWindow(this);
        try
        {
            var script = ProcessRunner.ResolveTestScript(_paths.Root);
            if (script is null)
            {
                UiHelpers.ShowError(Loc.T("tools.test_script_missing"), owner);
                return;
            }

            TestTargetsService.EnsureTargetsFile(_paths);
            var template = _runKind == PresetTestKind.Standard
                ? TestTargetsLoader.LoadDefinitions(_paths)
                : null;

            _ansi.Reset();
            _ansi.Clear(_output);
            _autoResponder = new TestScriptAutoResponder(_runKind, _scope, _batFiles, _terminal);
            _autoResponder.Reset();
            ResetTransitionState(_runKind);
            RunTracker.BeginRun(_runKind, template);
            RefreshVisualState();

            await _terminal.StartPowerShellScriptAsync(script, _paths.Root);
            _input.IsEnabled = true;
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(ex.Message, owner);
            RunTracker.StopRun();
            RefreshVisualState();
        }
    }

    private async Task StopTestAsync()
    {
        await _terminal.StopAsync();
        _ansi.Flush(_output);
        RunTracker.StopRun();
        _input.IsEnabled = false;
        RefreshVisualState();
        SaveSession();
    }

    private async Task ApplyBestAsync()
    {
        if (string.IsNullOrWhiteSpace(DisplayTracker.BestPresetFile)) return;
        await ApplyPresetAsync(DisplayTracker.BestPresetFile);
    }

    private async Task ApplyPresetAsync(string fileName)
    {
        var owner = Window.GetWindow(this);
        try
        {
            _settings.LastStrategy = fileName;
            _settings.Save();
            if (_strategy.IsRunning())
                await _strategy.StopStrategyAsync();
            await _strategy.StartStrategyAsync(fileName);
            if (Application.Current.MainWindow is MainWindow main)
                main.SyncHomeStrategy(fileName);
            UiHelpers.ShowInfo(Loc.F("tools.test_applied", StrategyDisplayHelper.ToDisplayName(fileName)), owner);
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(ex.Message, owner);
        }
    }

    private void OnTerminalOutput(string chunk) =>
        Dispatcher.InvokeAsync(async () =>
        {
            RunTracker.Feed(chunk);
            _ansi.Append(_output, chunk);
            if (_autoResponder is not null)
                await _autoResponder.FeedAsync(chunk);
        });

    private void OnTerminalError(string message) =>
        Dispatcher.InvokeAsync(() =>
            _ansi.Append(_output, $"{Loc.T("common.error_prefix")} {message}{Environment.NewLine}"));

    private void OnTerminalExited(int exitCode)
    {
        Dispatcher.Invoke(() =>
        {
            _ansi.Flush(_output);
            RunTracker.EndRun(exitCode);
            _input.IsEnabled = false;
            RefreshVisualState();
            SaveSession();
        });
    }

    private void OnTrackerChanged(PresetTestKind kind)
    {
        if (IsRunning && kind != _runKind) return;
        if (!IsRunning && kind != _displayKind) return;

        Dispatcher.Invoke(() =>
        {
            var active = IsRunning ? RunTracker : DisplayTracker;
            var view = IsRunning ? _viewStates[_runKind] : DisplayView;
            HandlePresetTransition(active, view);
            RefreshVisualState();
            RunStateChanged?.Invoke();
            SchedulePersist();
        });
    }

    private static Border CreateCard() =>
        new()
        {
            Background = (Brush)Application.Current.FindResource("PanelOverlayBrush"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16)
        };

    private static TextBlock MakeSectionTitle(string text) =>
        new()
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 4)
        };

    private static TextBlock MakeMuted(string text) =>
        new()
        {
            Text = text,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };

    private static Button MakeButton(string text, Func<Task> action)
    {
        var btn = new Button
        {
            Content = text,
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(14, 8, 14, 8)
        };
        btn.Click += async (_, _) => await action();
        return btn;
    }
}
