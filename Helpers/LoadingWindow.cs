using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aeroway.Helpers;

public sealed class LoadingWindow : Window
{
    public LoadingWindow(string message, Window? owner = null)
    {
        Title = Loc.T("common.loading_title");
        Width = 420;
        Height = 160;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = owner is null
            ? WindowStartupLocation.CenterScreen
            : WindowStartupLocation.CenterOwner;
        Owner = owner;
        Background = (Brush)Application.Current.FindResource("BgBrush");
        ShowInTaskbar = false;

        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 16)
        });
        root.Children.Add(new ProgressBar
        {
            IsIndeterminate = true,
            Height = 8,
            Foreground = (Brush)Application.Current.FindResource("AccentBrush"),
            Background = (Brush)Application.Current.FindResource("SurfaceLightBrush")
        });
        Content = root;
    }
}
