using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Aeroway.Helpers;
using Aeroway.Services;

namespace Aeroway.Pages;

public partial class DiagnosticsPage : UserControl
{
    private readonly ProcessRunner _runner;
    private RichTextBox _output = null!;

    public DiagnosticsPage(ProcessRunner runner)
    {
        _runner = runner;
        BuildUi();
        ConsoleLog.Instance.LineAdded += OnLineAdded;
        Unloaded += (_, _) => ConsoleLog.Instance.LineAdded -= OnLineAdded;
    }

    private void BuildUi()
    {
        var root = new DockPanel();

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(header, Dock.Top);
        header.Children.Add(new TextBlock
        {
            Text = Loc.T("tools.diagnostics"),
            FontSize = 28,
            FontWeight = FontWeights.Bold
        });
        header.Children.Add(new TextBlock
        {
            Text = Loc.T("tools.diagnostics_subtitle"),
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        });
        root.Children.Add(header);

        var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(toolbar, Dock.Top);
        toolbar.Children.Add(MakeButton(Loc.T("tools.diagnostics"), async () => await RunDiagnosticsAsync()));
        toolbar.Children.Add(MakeButton(Loc.T("tools.clear"), () =>
        {
            _output.Document.Blocks.Clear();
            AppendLine(Loc.T("tools.cleared"));
            return Task.CompletedTask;
        }));
        root.Children.Add(toolbar);

        _output = new RichTextBox
        {
            IsReadOnly = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Background = (Brush)Application.Current.FindResource("InputBrush"),
            Foreground = (Brush)Application.Current.FindResource("TextBrush"),
            BorderThickness = new Thickness(0),
            Document = new FlowDocument { PagePadding = new Thickness(4) }
        };
        AppendLine(Loc.T("tools.intro"));

        var border = new Border
        {
            Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10),
            Child = _output
        };
        root.Children.Add(border);

        Content = root;
    }

    private static Button MakeButton(string text, Func<Task> action)
    {
        var btn = new Button
        {
            Content = text,
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            Margin = new Thickness(0, 0, 8, 8)
        };
        btn.Click += async (_, _) => await action();
        return btn;
    }

    private void AppendLine(string line) => ColoredLog.AppendParagraph(_output, line);

    private void OnLineAdded(string line)
    {
        Dispatcher.Invoke(() =>
        {
            if (line == "__CLEAR__")
            {
                _output.Document.Blocks.Clear();
                return;
            }
            AppendLine(line);
        });
    }

    private async Task RunDiagnosticsAsync()
    {
        AppendLine("--- " + Loc.T("tools.diagnostics") + " ---");
        ConsoleLog.Instance.LineAdded -= OnLineAdded;
        try
        {
            var result = await UiHelpers.RunWithLoadingAsync(
                Window.GetWindow(this),
                Loc.T("common.loading"),
                () => _runner.RunBridgeAsync("RunDiagnostics"));
            if (!string.IsNullOrWhiteSpace(result))
            {
                foreach (var line in result.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
                    AppendLine(line);
            }
            AppendLine("--- " + Loc.T("tools.done") + " ---");

            if (UiHelpers.Confirm(Loc.T("diag.discord_cache_confirm"), Window.GetWindow(this)))
            {
                foreach (var line in DiscordCacheService.ClearCaches())
                    AppendLine(line);
            }
        }
        catch (Exception ex)
        {
            AppendLine($"{Loc.T("common.error_prefix")} {ex.Message}");
        }
        finally
        {
            ConsoleLog.Instance.LineAdded += OnLineAdded;
        }
    }
}
