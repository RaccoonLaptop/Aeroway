using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Aeroway.Helpers;

namespace Aeroway;

public sealed class UpdateInstallProgressWindow : Window
{
    private readonly string _logFile;
    private readonly TextBlock _status;
    private readonly TextBlock _details;
    private readonly ProgressBar _bar;
    private readonly DispatcherTimer _pollTimer;
    private long _logOffset;
    private bool _finished;
    private int _stallTicks;

    public UpdateInstallProgressWindow(string logFile, string targetVersion)
    {
        _logFile = logFile;
        Title = Loc.T("update.install_title");
        Width = 500;
        Height = 210;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Topmost = true;
        Background = (Brush)Application.Current.FindResource("BgBrush");

        var root = new StackPanel { Margin = new Thickness(22) };
        root.Children.Add(new TextBlock
        {
            Text = Loc.F("update.install_version", targetVersion),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        });

        _status = new TextBlock
        {
            Text = Loc.T("update.install_prep"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        };
        _details = new TextBlock
        {
            Text = Loc.T("update.install_dont_close"),
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _bar = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 10,
            Foreground = (Brush)Application.Current.FindResource("AccentBrush"),
            Background = (Brush)Application.Current.FindResource("SurfaceLightBrush")
        };

        root.Children.Add(_status);
        root.Children.Add(_details);
        root.Children.Add(_bar);
        Content = root;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _pollTimer.Tick += (_, _) => PollLog();
        Loaded += (_, _) => _pollTimer.Start();
        Closing += (_, e) =>
        {
            if (!_finished)
                e.Cancel = true;
        };
    }

    private void PollLog()
    {
        if (_finished) return;

        if (!File.Exists(_logFile))
        {
            _stallTicks++;
            if (_stallTicks > 40)
                Fail(Loc.T("update.install_no_response"));
            return;
        }

        _stallTicks = 0;
        try
        {
            using var stream = new FileStream(_logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (_logOffset > stream.Length)
                _logOffset = 0;
            stream.Seek(_logOffset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            var chunk = reader.ReadToEnd();
            _logOffset = stream.Length;
            if (string.IsNullOrWhiteSpace(chunk))
                return;

            foreach (var raw in chunk.Split('\n', '\r'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                ApplyLogLine(line);
            }
        }
        catch
        {
            /* log may be locked briefly */
        }
    }

    private void ApplyLogLine(string line)
    {
        if (line.Contains("ERROR:", StringComparison.OrdinalIgnoreCase))
        {
            var msg = line;
            var idx = line.IndexOf("ERROR:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                msg = line[(idx + "ERROR:".Length)..].Trim();
            Fail(FormatInstallError(msg));
            return;
        }

        if (line.Contains("completed", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("applied successfully", StringComparison.OrdinalIgnoreCase))
        {
            Complete();
            return;
        }

        var status = MapStatus(line);
        if (status is not null)
            _status.Text = status;
    }

    private static string FormatInstallError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Loc.T("update.install_failed");

        if (message.Contains("UPDATE_ACCESS_DENIED", StringComparison.OrdinalIgnoreCase))
            return Loc.T("update.install_access_denied");

        return message;
    }

    private static string? MapStatus(string line)
    {
        if (line.Contains("Waiting for", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Waiting for application", StringComparison.OrdinalIgnoreCase))
            return Loc.T("update.install_closing");

        if (line.Contains("Stopping bypass", StringComparison.OrdinalIgnoreCase))
            return Loc.T("update.install_stopping_bypass");

        if (line.Contains("Installer update started", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Updater started", StringComparison.OrdinalIgnoreCase))
            return Loc.T("update.install_launching");

        if (line.Contains("Running:", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Установка обновления", StringComparison.OrdinalIgnoreCase))
            return Loc.T("update.install_copying");

        if (line.Contains("Remove:", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Copy", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Preserve:", StringComparison.OrdinalIgnoreCase))
            return Loc.T("update.install_updating_files");

        if (line.Contains("Starting:", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Запуск Aeroway", StringComparison.OrdinalIgnoreCase))
            return Loc.T("update.install_restarting");

        return null;
    }

    private void Complete()
    {
        if (_finished) return;
        _finished = true;
        _pollTimer.Stop();
        _status.Text = Loc.T("update.install_complete");
        _details.Text = Loc.T("update.install_auto_close");
        _bar.IsIndeterminate = false;
        _bar.Value = 100;
        var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        closeTimer.Tick += (_, _) =>
        {
            closeTimer.Stop();
            Close();
            Application.Current.Shutdown();
        };
        closeTimer.Start();
    }

    private void Fail(string message)
    {
        if (_finished) return;
        _finished = true;
        _pollTimer.Stop();
        _status.Text = Loc.T("update.install_failed_title");
        _details.Text = message;
        _details.Foreground = (Brush)Application.Current.FindResource("ErrorBrush");
        _bar.IsIndeterminate = false;
        _bar.Value = 0;

        var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        closeTimer.Tick += (_, _) =>
        {
            closeTimer.Stop();
            Close();
            Application.Current.Shutdown();
        };
        closeTimer.Start();
    }
}

