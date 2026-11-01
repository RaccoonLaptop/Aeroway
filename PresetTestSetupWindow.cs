using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aeroway.Helpers;
using Aeroway.Models;

namespace Aeroway;

public sealed class PresetTestSetupWindow : Window
{
    private readonly Border _allCard;
    private readonly List<(Border Row, StrategyItem Item)> _strategyRows = [];
    private bool _allMode = true;

    public bool Confirmed { get; private set; }

    private PresetTestSetupWindow(IReadOnlyList<StrategyItem> strategies)
    {
        Title = Loc.T("tools.test_setup_title");
        Width = 540;
        Height = 580;
        MinWidth = 540;
        MaxWidth = 540;
        MinHeight = 580;
        MaxHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.FindResource("BgBrush");
        ResizeMode = ResizeMode.NoResize;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = Loc.T("tools.test_setup_title"),
            FontSize = 20,
            FontWeight = FontWeights.Bold
        });
        header.Children.Add(new TextBlock
        {
            Text = Loc.T("tools.test_setup_subtitle"),
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _allCard = CreateSelectableCard(Loc.T("tools.test_scope_all"), null, true);
        _allCard.Margin = new Thickness(0, 16, 0, 10);
        _allCard.MouseLeftButtonUp += (_, _) => SetAllMode(true);
        Grid.SetRow(_allCard, 1);
        root.Children.Add(_allCard);

        var listLabel = new TextBlock
        {
            Text = Loc.T("tools.test_scope_list_title"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(listLabel, 2);
        root.Children.Add(listLabel);

        var listBorder = new Border
        {
            Background = (Brush)Application.Current.FindResource("InputBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(6)
        };
        var listScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var listStack = new StackPanel();
        foreach (var item in strategies)
        {
            var row = CreateSelectableCard(item.DisplayName, item, false);
            row.Margin = new Thickness(0, 0, 0, 4);
            row.Opacity = 0.55;
            row.MouseLeftButtonUp += (_, _) => ToggleStrategy(row, item);
            _strategyRows.Add((row, item));
            listStack.Children.Add(row);
        }
        listScroll.Content = listStack;
        listBorder.Child = listScroll;
        Grid.SetRow(listBorder, 3);
        root.Children.Add(listBorder);

        var hint = new TextBlock
        {
            Text = Loc.T("tools.test_scope_pick_hint"),
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        };
        Grid.SetRow(hint, 4);
        root.Children.Add(hint);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var ok = new Button
        {
            Content = Loc.T("tools.test_setup_start"),
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true,
            MinWidth = 130,
            Padding = new Thickness(16, 10, 16, 10)
        };
        ok.Click += (_, _) =>
        {
            if (!_allMode && !HasAnySelected())
            {
                UiHelpers.ShowError(Loc.T("tools.test_scope_pick"), this);
                return;
            }
            Confirmed = true;
            Close();
        };
        var cancel = new Button
        {
            Content = Loc.T("common.cancel"),
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            IsCancel = true,
            MinWidth = 100,
            Padding = new Thickness(16, 10, 16, 10)
        };
        cancel.Click += (_, _) => Close();
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        Grid.SetRow(buttons, 5);
        root.Children.Add(buttons);

        Content = root;
    }

    private static Border CreateSelectableCard(string title, StrategyItem? item, bool selected)
    {
        var card = new Border
        {
            Background = (Brush)Application.Current.FindResource(selected ? "PanelOverlayBrush" : "InputBrush"),
            BorderBrush = (Brush)Application.Current.FindResource(selected ? "AccentBrush" : "BorderBrush"),
            BorderThickness = new Thickness(selected ? 2 : 1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 11, 14, 11),
            Cursor = Cursors.Hand,
            Tag = item
        };
        card.Child = new TextBlock
        {
            Text = title,
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap
        };
        return card;
    }

    private void SetAllMode(bool all)
    {
        _allMode = all;
        ApplyCardStyle(_allCard, all);

        foreach (var (row, _) in _strategyRows)
        {
            if (all)
            {
                ApplyCardStyle(row, false);
                row.Opacity = 0.55;
            }
            else
            {
                row.Opacity = 1;
            }
        }
    }

    private void ToggleStrategy(Border row, StrategyItem item)
    {
        if (_allMode)
        {
            SetAllMode(false);
            ApplyCardStyle(row, true);
            return;
        }

        var selected = !IsRowSelected(row);
        ApplyCardStyle(row, selected);

        if (!HasAnySelected())
            SetAllMode(true);
    }

    private static void ApplyCardStyle(Border card, bool selected)
    {
        card.Background = (Brush)Application.Current.FindResource(selected ? "PanelOverlayBrush" : "InputBrush");
        card.BorderBrush = (Brush)Application.Current.FindResource(selected ? "AccentBrush" : "BorderBrush");
        card.BorderThickness = new Thickness(selected ? 2 : 1);
        if (card.Child is TextBlock text)
            text.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private static bool IsRowSelected(Border row) =>
        row.BorderThickness.Left > 1;

    private bool HasAnySelected() =>
        _strategyRows.Any(entry => IsRowSelected(entry.Row));

    public static bool TryShow(Window? owner, IReadOnlyList<StrategyItem> strategies, out PresetTestScope scope)
    {
        scope = new PresetTestScope();
        if (strategies.Count == 0)
        {
            UiHelpers.ShowError(Loc.T("tools.test_no_strategies"), owner);
            return false;
        }

        var dlg = new PresetTestSetupWindow(strategies) { Owner = owner };
        dlg.ShowDialog();
        if (!dlg.Confirmed) return false;

        var testAll = dlg._allMode;
        var selected = testAll
            ? (IReadOnlyList<string>)[]
            : dlg._strategyRows
                .Where(entry => IsRowSelected(entry.Row))
                .Select(entry => entry.Item.FileName)
                .ToList();

        scope = new PresetTestScope
        {
            TestAll = testAll,
            SelectedStrategyFiles = selected
        };
        return true;
    }
}
