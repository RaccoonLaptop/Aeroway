using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aeroway.Helpers;

public sealed class ConfirmWindow : Window
{
    public bool Confirmed { get; private set; }

    public ConfirmWindow(string message, Window? owner = null)
    {
        Title = Loc.T("app.title");
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

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var noBtn = new Button
        {
            Content = Loc.T("common.no"),
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            MinWidth = 96,
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true
        };
        noBtn.Click += (_, _) =>
        {
            Confirmed = false;
            Close();
        };

        var yesBtn = new Button
        {
            Content = Loc.T("common.yes"),
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            MinWidth = 96,
            IsDefault = true
        };
        yesBtn.Click += (_, _) =>
        {
            Confirmed = true;
            Close();
        };

        buttons.Children.Add(noBtn);
        buttons.Children.Add(yesBtn);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

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

    public static bool Show(string message, Window? owner = null)
    {
        var dialog = new ConfirmWindow(message, owner);
        dialog.ShowDialog();
        return dialog.Confirmed;
    }
}
