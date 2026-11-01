using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aeroway.Helpers;

public sealed class MessageWindow : Window
{
    public MessageWindow(string message, string? title = null, Window? owner = null)
    {
        Title = title ?? Loc.T("app.title");
        Width = 460;
        SizeToContent = SizeToContent.Height;
        MinWidth = 360;
        MaxWidth = 560;
        WindowStartupLocation = owner is null
            ? WindowStartupLocation.CenterScreen
            : WindowStartupLocation.CenterOwner;
        Owner = owner;
        ResizeMode = ResizeMode.NoResize;
        Background = (Brush)Application.Current.FindResource("BgBrush");
        Foreground = (Brush)Application.Current.FindResource("TextBrush");

        var root = new DockPanel { Margin = new Thickness(20) };

        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            LineHeight = 22,
            Margin = new Thickness(0, 0, 0, 20)
        };
        DockPanel.SetDock(messageBlock, Dock.Top);
        root.Children.Add(messageBlock);

        var okBtn = new Button
        {
            Content = Loc.T("common.ok"),
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            MinWidth = 96,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true
        };
        okBtn.Click += (_, _) => Close();
        DockPanel.SetDock(okBtn, Dock.Bottom);
        root.Children.Add(okBtn);

        var card = new Border
        {
            Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Child = root
        };

        Content = new Border
        {
            Background = (Brush)Application.Current.FindResource("BgBrush"),
            Padding = new Thickness(8),
            Child = card
        };
    }

    public static void Show(string message, string? title = null, Window? owner = null)
    {
        var dialog = new MessageWindow(message, title, owner);
        dialog.ShowDialog();
    }
}
