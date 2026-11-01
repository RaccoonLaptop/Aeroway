using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Aeroway.Helpers;

public sealed class ResultWindow : Window
{
    private readonly RichTextBox _output;

    public ResultWindow(string title, string? text = null)
    {
        Title = title;
        Width = 560;
        Height = 420;
        MinWidth = 400;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.FindResource("BgBrush");

        var root = new DockPanel { Margin = new Thickness(16) };

        var header = new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var closeBtn = new Button
        {
            Content = Loc.T("common.close"),
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 100,
            IsDefault = true
        };
        closeBtn.Click += (_, _) => Close();
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        btnPanel.Children.Add(closeBtn);
        DockPanel.SetDock(btnPanel, Dock.Bottom);
        root.Children.Add(btnPanel);

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
            Document = new FlowDocument { PagePadding = new Thickness(8) }
        };

        var border = new Border
        {
            Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = _output
        };
        root.Children.Add(border);

        Content = root;

        if (!string.IsNullOrWhiteSpace(text))
            SetText(text);
    }

    public void SetText(string text)
    {
        _output.Document.Blocks.Clear();
        foreach (var line in text.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (string.IsNullOrEmpty(line)) continue;
            ColoredLog.AppendParagraph(_output, line);
        }
    }

    public static void Show(Window? owner, string title, string text)
    {
        var w = new ResultWindow(title, text) { Owner = owner };
        w.ShowDialog();
    }
}
