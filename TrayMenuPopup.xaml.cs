using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using System.Windows.Media;
using Aeroway.Helpers;

namespace Aeroway;

public partial class TrayMenuPopup : Window
{
    private readonly Func<Task> _toggleBypassAsync;
    private readonly Action _openWindow;
    private readonly Action _exitApp;
    private readonly Func<IReadOnlyList<StrategyItem>> _getStrategies;
    private readonly Func<string?> _getSelectedStrategy;
    private readonly Func<string, Task> _switchStrategyAsync;
    private bool _running;
    private bool _busy;
    private bool _suppressStrategyChange;

    public TrayMenuPopup(
        Func<Task> toggleBypassAsync,
        Action openWindow,
        Action exitApp,
        Func<IReadOnlyList<StrategyItem>> getStrategies,
        Func<string?> getSelectedStrategy,
        Func<string, Task> switchStrategyAsync)
    {
        _toggleBypassAsync = toggleBypassAsync;
        _openWindow = openWindow;
        _exitApp = exitApp;
        _getStrategies = getStrategies;
        _getSelectedStrategy = getSelectedStrategy;
        _switchStrategyAsync = switchStrategyAsync;
        InitializeComponent();
        StrategyCombo.DisplayMemberPath = nameof(StrategyItem.DisplayName);
        StrategyCombo.SelectedValuePath = nameof(StrategyItem.FileName);
        ApplyLocalization();
        Deactivated += (_, _) => Hide();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        };
    }

    public void UpdateState(bool running, string? strategyTitle, bool busy)
    {
        _running = running;
        _busy = busy;

        StatusDot.Fill = busy
            ? (Brush)FindResource("WarningBrush")
            : running
                ? (Brush)FindResource("SuccessBrush")
                : (Brush)FindResource("ErrorBrush");

        StatusText.Text = busy
            ? Loc.T("tray.status.starting")
            : (running ? Loc.T("status.running") : Loc.T("status.stopped"));

        var hasStrategy = running && !string.IsNullOrWhiteSpace(strategyTitle);
        StrategyText.Visibility = hasStrategy ? Visibility.Visible : Visibility.Collapsed;
        StrategyText.Text = hasStrategy ? StrategyDisplayHelper.ToDisplayName(strategyTitle!) : "";

        ToggleBtn.Content = busy
            ? Loc.T("home.starting")
            : (running ? Loc.T("tray.toggle.stop") : Loc.T("tray.toggle.start"));
        ToggleBtn.IsEnabled = !busy;
        ToggleBtn.Foreground = running
            ? (Brush)FindResource("ErrorBrush")
            : (Brush)FindResource("AccentBrush");

        OpenBtn.Content = Loc.T("tray.open");
        ExitBtn.Content = Loc.T("tray.exit");

        PopulateStrategyCombo();
    }

    private void PopulateStrategyCombo()
    {
        _suppressStrategyChange = true;
        var selected = _getSelectedStrategy();
        StrategyCombo.Items.Clear();
        foreach (var strategy in _getStrategies())
            StrategyCombo.Items.Add(strategy);

        if (!string.IsNullOrEmpty(selected))
        {
            foreach (StrategyItem item in StrategyCombo.Items)
            {
                if (item.FileName.Equals(selected, StringComparison.OrdinalIgnoreCase))
                {
                    StrategyCombo.SelectedItem = item;
                    break;
                }
            }
        }

        if (StrategyCombo.SelectedItem is null && StrategyCombo.Items.Count > 0)
            StrategyCombo.SelectedIndex = 0;

        StrategyCombo.IsEnabled = !_busy;
        _suppressStrategyChange = false;
    }

    public void ShowNearCursor()
    {
        ApplyLocalization();

        if (!IsLoaded)
        {
            Show();
            Hide();
        }

        UpdateLayout();
        var menuH = ActualHeight > 1 ? ActualHeight : Height;
        var menuW = ActualWidth > 1 ? ActualWidth : Width;

        var cursor = Forms.Control.MousePosition;
        var dip = ScreenToDip(cursor.X, cursor.Y);

        Left = dip.X;
        Top = dip.Y - menuH - 8;

        var work = SystemParameters.WorkArea;
        if (Left + menuW > work.Right - 4)
            Left = work.Right - menuW - 4;
        if (Left < work.Left + 4)
            Left = work.Left + 4;
        if (Top < work.Top + 4)
            Top = dip.Y + 8;

        if (!IsVisible)
            Show();
        Activate();
        Focus();
    }

    private Point ScreenToDip(int screenX, int screenY)
    {
        var source = PresentationSource.FromVisual(this)
                     ?? PresentationSource.FromVisual(Application.Current.MainWindow!);
        if (source?.CompositionTarget is null)
            return new Point(screenX, screenY);

        return source.CompositionTarget.TransformFromDevice.Transform(new Point(screenX, screenY));
    }

    private void ApplyLocalization()
    {
        StrategyLabel.Text = Loc.T("tray.strategy_label");
        OpenBtn.Content = Loc.T("tray.open");
        ExitBtn.Content = Loc.T("tray.exit");
    }

    private async void StrategyCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_suppressStrategyChange) return;
        if (StrategyCombo.SelectedItem is not StrategyItem strategy) return;

        StrategyCombo.IsEnabled = false;
        try
        {
            Hide();
            await _switchStrategyAsync(strategy.FileName);
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(ex.Message);
        }
        finally
        {
            StrategyCombo.IsEnabled = !_busy;
        }
    }

    private async void ToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        ToggleBtn.IsEnabled = false;
        try
        {
            Hide();
            await _toggleBypassAsync();
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(ex.Message);
        }
        finally
        {
            ToggleBtn.IsEnabled = !_busy;
        }
    }

    private void OpenBtn_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _openWindow();
    }

    private void ExitBtn_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _exitApp();
    }
}
