using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aeroway.Helpers;
using Aeroway.Services;

namespace Aeroway;

public sealed class BootstrapWindow : Window
{
    private readonly string _targetDir;
    private readonly TextBlock _statusText;
    private readonly ProgressBar _progressBar;
    private readonly Button _retryButton;
    private CancellationTokenSource? _cts;

    public BootstrapWindow(string targetDir)
    {
        _targetDir = targetDir;

        Title = Loc.T("bootstrap.title");
        Width = 520;
        Height = 280;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = (Brush)Application.Current.FindResource("BgBrush");
        AppIcon.ApplyTo(this);

        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(new TextBlock
        {
            Text = Loc.T("bootstrap.heading"),
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        root.Children.Add(new TextBlock
        {
            Text = Loc.T("bootstrap.desc"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 20)
        });

        _progressBar = new ProgressBar
        {
            Height = 8,
            IsIndeterminate = true,
            Margin = new Thickness(0, 0, 0, 12)
        };
        root.Children.Add(_progressBar);

        _statusText = new TextBlock
        {
            Text = Loc.T("bootstrap.prep"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 16)
        };
        root.Children.Add(_statusText);

        _retryButton = new Button
        {
            Content = Loc.T("bootstrap.retry"),
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 8)
        };
        _retryButton.Click += (_, _) => _ = RunBootstrapAsync();
        root.Children.Add(_retryButton);

        Content = root;
        Loaded += (_, _) => _ = RunBootstrapAsync();
        Closing += (_, e) =>
        {
            if (_cts is { IsCancellationRequested: false } && DialogResult != true)
            {
                _cts.Cancel();
            }
        };
    }

    private async Task RunBootstrapAsync()
    {
        _retryButton.Visibility = Visibility.Collapsed;
        _progressBar.IsIndeterminate = true;
        _statusText.Text = "Подключение к GitHub...";

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var service = new ZapretBootstrapService();
        var progress = new Progress<string>(msg => _statusText.Text = msg);
        var result = await service.EnsureInstalledAsync(_targetDir, progress, _cts.Token);

        if (result.Success)
        {
            _statusText.Text = result.Message;
            _progressBar.IsIndeterminate = false;
            _progressBar.Value = 100;
            DialogResult = true;
            Close();
            return;
        }

        _progressBar.IsIndeterminate = false;
        _statusText.Text = result.Message;
        _retryButton.Visibility = Visibility.Visible;
    }
}
