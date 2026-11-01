using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aeroway.Services;

namespace Aeroway.Helpers;

public sealed class FakeReplacementWindow : Window
{
    public bool Applied { get; private set; }
    public FakeTarget SelectedTarget { get; private set; }
    public string? SelectedFile { get; private set; }

    public FakeReplacementWindow(FakeReplacementStatus status, Window? owner = null)
    {
        Title = Loc.T("fake.title");
        Width = 520;
        SizeToContent = SizeToContent.Height;
        MinWidth = 420;
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        Owner = owner;
        ResizeMode = ResizeMode.NoResize;
        Background = (Brush)Application.Current.FindResource("BgBrush");
        Foreground = (Brush)Application.Current.FindResource("TextBrush");

        var root = new StackPanel { Margin = new Thickness(20) };

        root.Children.Add(new TextBlock
        {
            Text = Loc.T("fake.subtitle"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 12)
        });

        root.Children.Add(MakeInfo(Loc.F("fake.current_discord", status.CurrentDiscordFake ?? Loc.T("fake.not_found"))));
        root.Children.Add(MakeInfo(Loc.F("fake.current_game", status.CurrentGameFake ?? Loc.T("fake.not_found"))));

        root.Children.Add(new TextBlock
        {
            Text = Loc.T("fake.type_label"),
            Margin = new Thickness(0, 12, 0, 6)
        });
        var typeCombo = new ComboBox
        {
            MinWidth = 280,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        typeCombo.Items.Add(Loc.T("fake.type_discord"));
        typeCombo.Items.Add(Loc.T("fake.type_game"));
        typeCombo.SelectedIndex = 0;
        root.Children.Add(typeCombo);

        root.Children.Add(new TextBlock
        {
            Text = Loc.T("fake.file_label"),
            Margin = new Thickness(0, 12, 0, 6)
        });
        var fileCombo = new ComboBox
        {
            MinWidth = 280,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        foreach (var file in status.AvailableFiles)
            fileCombo.Items.Add(file);
        if (fileCombo.Items.Count > 0)
            fileCombo.SelectedIndex = 0;
        root.Children.Add(fileCombo);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };
        var cancelBtn = new Button
        {
            Content = Loc.T("common.cancel"),
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            MinWidth = 96,
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true
        };
        cancelBtn.Click += (_, _) => Close();
        var applyBtn = new Button
        {
            Content = Loc.T("fake.apply"),
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            MinWidth = 96,
            IsDefault = true,
            IsEnabled = fileCombo.Items.Count > 0
        };
        applyBtn.Click += (_, _) =>
        {
            SelectedTarget = typeCombo.SelectedIndex == 1 ? FakeTarget.GameFilterUdp : FakeTarget.DiscordUdp;
            SelectedFile = fileCombo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(SelectedFile))
                return;
            Applied = true;
            Close();
        };
        buttons.Children.Add(cancelBtn);
        buttons.Children.Add(applyBtn);
        root.Children.Add(buttons);

        Content = new Border
        {
            Background = (Brush)Application.Current.FindResource("BgBrush"),
            Padding = new Thickness(8),
            Child = new Border
            {
                Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
                BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                Child = root
            }
        };
    }

    private static TextBlock MakeInfo(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 4)
    };

    public static bool TryShow(FakeReplacementStatus status, out FakeTarget target, out string? file, Window? owner = null)
    {
        target = FakeTarget.DiscordUdp;
        file = null;
        var dialog = new FakeReplacementWindow(status, owner);
        dialog.ShowDialog();
        if (!dialog.Applied || string.IsNullOrWhiteSpace(dialog.SelectedFile))
            return false;
        target = dialog.SelectedTarget;
        file = dialog.SelectedFile;
        return true;
    }
}
