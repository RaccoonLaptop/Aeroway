using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aeroway.Helpers;
using Aeroway.Models;
using Aeroway.Services;

namespace Aeroway.Pages;

public partial class TestStrategiesPage : UserControl
{
    private readonly ZapretPaths _paths;
    private readonly StrategyService _strategy;
    private readonly AppSettings _settings;
    private Border _modeStandard = null!;
    private Border _modeDpi = null!;
    private Button _startBtn = null!;
    private PresetTestRunPanel _runPanel = null!;
    private PresetTestKind _selectedKind = PresetTestKind.Standard;
    private Style _primaryBtnStyle = null!;
    private Style _dangerBtnStyle = null!;

    public TestStrategiesPage(ZapretPaths paths, StrategyService strategy, AppSettings settings)
    {
        _paths = paths;
        _strategy = strategy;
        _settings = settings;
        VerticalAlignment = VerticalAlignment.Stretch;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        BuildUi();
    }

    private void BuildUi()
    {
        _primaryBtnStyle = (Style)Application.Current.FindResource("PrimaryButton");
        _dangerBtnStyle = (Style)Application.Current.FindResource("DangerButton");

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 0 });

        var title = new TextBlock
        {
            Text = Loc.T("tools.test"),
            FontSize = 28,
            FontWeight = FontWeights.Bold
        };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var subtitle = new TextBlock
        {
            Text = Loc.T("tools.test_subtitle_visual"),
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 16)
        };
        Grid.SetRow(subtitle, 1);
        root.Children.Add(subtitle);

        var modeRow = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        modeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        modeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        modeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _modeStandard = CreateModeCard(Loc.T("tools.test_mode_standard_title"), Loc.T("tools.test_mode_standard_hint"), true);
        _modeStandard.MouseLeftButtonUp += (_, _) => SelectMode(PresetTestKind.Standard);
        Grid.SetColumn(_modeStandard, 0);
        modeRow.Children.Add(_modeStandard);
        _modeDpi = CreateModeCard(Loc.T("tools.test_mode_dpi_title"), Loc.T("tools.test_mode_dpi_hint"), false);
        _modeDpi.MouseLeftButtonUp += (_, _) => SelectMode(PresetTestKind.DpiFreeze);
        Grid.SetColumn(_modeDpi, 2);
        modeRow.Children.Add(_modeDpi);
        Grid.SetRow(modeRow, 2);
        root.Children.Add(modeRow);

        _startBtn = new Button
        {
            Content = Loc.T("tools.test_start"),
            Style = _primaryBtnStyle,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 180,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(20, 12, 20, 12)
        };
        _startBtn.Click += async (_, _) => await OnStartStopClickedAsync();
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        actions.Children.Add(_startBtn);
        if (AppBuild.IsDevBuild)
        {
            var pickBtn = new Button
            {
                Content = Loc.T("pick.open"),
                Style = (Style)Application.Current.FindResource("SecondaryButton"),
                MinWidth = 180,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(20, 12, 20, 12),
                ToolTip = Loc.T("pick.dev_only")
            };
            pickBtn.Click += (_, _) => OpenStrategyPick();
            actions.Children.Add(pickBtn);
        }
        Grid.SetRow(actions, 3);
        root.Children.Add(actions);

        _runPanel = new PresetTestRunPanel(_paths, _strategy, _settings)
        {
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _runPanel.RunStateChanged += UpdateStartButtonState;
        Grid.SetRow(_runPanel, 4);
        root.Children.Add(_runPanel);

        Content = root;
        var restoredKind = _runPanel.RestoreFromStorage();
        SelectMode(restoredKind);
    }

    public void SaveSession() => _runPanel.SaveSession();

    public async Task DisposePanelAsync() => await _runPanel.DisposeTerminalAsync();

    private void OpenStrategyPick()
    {
        if (!AppBuild.IsDevBuild)
            return;
        if (_runPanel.IsRunning)
        {
            UiHelpers.ShowInfo(Loc.T("pick.busy"), Window.GetWindow(this));
            return;
        }

        StrategyPickWindow.TryShow(Window.GetWindow(this), _paths, _strategy, _settings);
    }

    private async Task OnStartStopClickedAsync()
    {
        if (_runPanel.IsRunning)
        {
            await _runPanel.StopAsync();
            return;
        }

        await BeginTestFlowAsync();
    }

    private async Task BeginTestFlowAsync()
    {
        var strategies = StrategyDisplayHelper.LoadItems(_paths.Root, _paths.GetStrategyFiles());
        var owner = Window.GetWindow(this);
        if (!PresetTestSetupWindow.TryShow(owner, strategies, out var scope))
            return;

        _runPanel.Visibility = Visibility.Visible;
        UpdateStartButtonState();
        await _runPanel.StartAsync(_selectedKind, scope);
        if (_runPanel.HasVisibleContent)
            _runPanel.Visibility = Visibility.Visible;
    }

    private void UpdateStartButtonState()
    {
        var running = _runPanel.IsRunning;
        _startBtn.Content = running ? Loc.T("tools.test_stop_run") : Loc.T("tools.test_start");
        _startBtn.Style = running ? _dangerBtnStyle : _primaryBtnStyle;
        _modeStandard.IsHitTestVisible = !running;
        _modeDpi.IsHitTestVisible = !running;
        _modeStandard.Opacity = running ? 0.55 : 1;
        _modeDpi.Opacity = running ? 0.55 : 1;
    }

    private void SelectMode(PresetTestKind kind)
    {
        if (_runPanel.IsRunning) return;
        _selectedKind = kind;
        SetModeSelected(_modeStandard, kind == PresetTestKind.Standard);
        SetModeSelected(_modeDpi, kind == PresetTestKind.DpiFreeze);
        _runPanel.SwitchDisplayKind(kind);
        _runPanel.Visibility = _runPanel.HasVisibleContent
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static Border CreateModeCard(string title, string hint, bool selected)
    {
        var card = new Border
        {
            Background = (Brush)Application.Current.FindResource("InputBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Cursor = Cursors.Hand
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
        stack.Children.Add(new TextBlock
        {
            Text = hint,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        card.Child = stack;
        SetModeSelected(card, selected);
        return card;
    }

    private static void SetModeSelected(Border card, bool selected)
    {
        card.BorderBrush = (Brush)Application.Current.FindResource(selected ? "AccentBrush" : "BorderBrush");
        card.BorderThickness = new Thickness(selected ? 2 : 1);
        card.Background = (Brush)Application.Current.FindResource(selected ? "PanelOverlayBrush" : "InputBrush");
    }
}
