using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aeroway.Services;

namespace Aeroway;

public sealed class UpdateDownloadWindow : Window
{
    private readonly TextBlock _status;
    private readonly TextBlock _details;
    private readonly ProgressBar _bar;

    public UpdateDownloadWindow(string title, string message)
    {
        Title = title;
        Width = 480;
        Height = 200;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.FindResource("BgBrush");
        ShowInTaskbar = false;

        var root = new StackPanel { Margin = new Thickness(20) };
        _status = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        };
        _details = new TextBlock
        {
            Text = "",
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10)
        };
        _bar = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 10,
            Minimum = 0,
            Maximum = 100,
            Foreground = (Brush)Application.Current.FindResource("AccentBrush"),
            Background = (Brush)Application.Current.FindResource("SurfaceLightBrush")
        };
        root.Children.Add(_status);
        root.Children.Add(_details);
        root.Children.Add(_bar);
        Content = root;
    }

    public void SetStatus(string text) => _status.Text = text;

    public void ReportProgress(DownloadProgress progress)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => ReportProgress(progress));
            return;
        }

        if (!string.IsNullOrWhiteSpace(progress.Phase))
            _status.Text = progress.Phase;

        var received = FormatBytes(progress.BytesReceived);
        var total = progress.TotalBytes is > 0
            ? FormatBytes(progress.TotalBytes.Value)
            : "—";
        var speed = progress.BytesPerSecond > 0
            ? $"{FormatBytes((long)progress.BytesPerSecond)}/с"
            : "—";

        _details.Text = $"{received} / {total}  ·  {speed}";

        if (progress.Percent >= 0)
        {
            _bar.IsIndeterminate = false;
            _bar.Value = progress.Percent;
        }
        else
        {
            _bar.IsIndeterminate = true;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.0} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.0} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):0.00} GB";
    }
}
