using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;

namespace Aeroway.Helpers;

public sealed class FindReplaceDialog : Window
{
    private readonly TextEditor _editor;
    private readonly TextBox _findBox;
    private readonly TextBox _replaceBox;
    private readonly CheckBox _matchCase;
    private int _lastIndex;

    public FindReplaceDialog(TextEditor editor, Window? owner)
    {
        _editor = editor;
        Owner = owner;
        Title = Loc.T("findreplace.title");
        Width = 520;
        SizeToContent = SizeToContent.Height;
        MinHeight = 300;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.FindResource("BgBrush");
        AppIcon.ApplyTo(this);

        var root = new StackPanel { Margin = new Thickness(20, 20, 20, 28) };
        root.Children.Add(Label(Loc.T("findreplace.find_label")));
        _findBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
        root.Children.Add(_findBox);

        root.Children.Add(Label(Loc.T("findreplace.replace_label")));
        _replaceBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
        root.Children.Add(_replaceBox);

        _matchCase = new CheckBox
        {
            Content = Loc.T("findreplace.match_case"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        root.Children.Add(_matchCase);

        var findRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        findRow.Children.Add(MakeBtn(Loc.T("findreplace.find_next"), FindNext));
        findRow.Children.Add(MakeBtn(Loc.T("findreplace.find_prev"), FindPrevious));
        root.Children.Add(findRow);

        var replRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        replRow.Children.Add(MakeBtn(Loc.T("findreplace.replace_one"), ReplaceOne));
        replRow.Children.Add(MakeBtn(Loc.T("findreplace.replace_all"), ReplaceAll));
        root.Children.Add(replRow);

        var closeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 4)
        };
        var close = MakeBtn(Loc.T("common.close"), (_, _) => Close());
        close.IsCancel = true;
        closeRow.Children.Add(close);
        root.Children.Add(closeRow);

        Content = root;
        Loaded += (_, _) =>
        {
            _findBox.Focus();
            if (!string.IsNullOrEmpty(_editor.SelectedText))
                _findBox.Text = _editor.SelectedText;
            else
                _lastIndex = _editor.CaretOffset;
        };

        _findBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { FindNext(null!, e); e.Handled = true; }
        };
        _replaceBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { ReplaceOne(null!, e); e.Handled = true; }
        };
    }

    public static void ShowFor(TextEditor editor, Window? owner)
    {
        var dlg = new FindReplaceDialog(editor, owner);
        dlg.Show();
    }

    private void FindNext(object sender, RoutedEventArgs e) => Find(forward: true);

    private void FindPrevious(object sender, RoutedEventArgs e) => Find(forward: false);

    private void Find(bool forward)
    {
        var needle = _findBox.Text;
        if (string.IsNullOrEmpty(needle))
        {
            UiHelpers.ShowInfo(Loc.T("findreplace.enter_search"));
            return;
        }

        var text = _editor.Text;
        var cmp = _matchCase.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var start = forward
            ? Math.Max(_lastIndex, _editor.CaretOffset)
            : Math.Min(_lastIndex, _editor.CaretOffset);

        int index;
        if (forward)
        {
            index = text.IndexOf(needle, start, cmp);
            if (index < 0 && start > 0)
                index = text.IndexOf(needle, 0, cmp);
        }
        else
        {
            index = start > 0
                ? text.LastIndexOf(needle, start - 1, cmp)
                : -1;
            if (index < 0)
                index = text.LastIndexOf(needle, text.Length - 1, cmp);
        }

        if (index < 0)
        {
            UiHelpers.ShowInfo(Loc.T("findreplace.not_found"));
            return;
        }

        SelectMatch(index, needle.Length);
        _lastIndex = forward ? index + needle.Length : index;
    }

    private void ReplaceOne(object sender, RoutedEventArgs e)
    {
        var needle = _findBox.Text;
        if (string.IsNullOrEmpty(needle)) return;

        var selStart = _editor.SelectionStart;
        var selLen = _editor.SelectionLength;
        var cmp = _matchCase.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        if (selLen > 0 &&
            string.Equals(_editor.SelectedText, needle, cmp))
        {
            _editor.Document.Replace(selStart, selLen, _replaceBox.Text);
            _lastIndex = selStart + _replaceBox.Text.Length;
            FindNext(sender, e);
            return;
        }

        FindNext(sender, e);
        if (_editor.SelectionLength > 0 &&
            string.Equals(_editor.SelectedText, needle, cmp))
        {
            selStart = _editor.SelectionStart;
            selLen = _editor.SelectionLength;
            _editor.Document.Replace(selStart, selLen, _replaceBox.Text);
            _lastIndex = selStart + _replaceBox.Text.Length;
        }
    }

    private void ReplaceAll(object sender, RoutedEventArgs e)
    {
        var needle = _findBox.Text;
        if (string.IsNullOrEmpty(needle)) return;

        var cmp = _matchCase.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var count = 0;
        var offset = 0;
        while (true)
        {
            var idx = _editor.Text.IndexOf(needle, offset, cmp);
            if (idx < 0) break;
            _editor.Document.Replace(idx, needle.Length, _replaceBox.Text);
            offset = idx + _replaceBox.Text.Length;
            count++;
        }

        UiHelpers.ShowInfo(count > 0 ? Loc.F("findreplace.replaced_count", count) : Loc.T("findreplace.not_found"));
    }

    private void SelectMatch(int index, int length)
    {
        _editor.Select(index, length);
        _editor.CaretOffset = index + length;
        var line = _editor.Document.GetLineByOffset(index);
        _editor.ScrollTo(line.LineNumber, 1);
        _editor.Focus();
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        Margin = new Thickness(0, 0, 0, 4)
    };

    private static Button MakeBtn(string text, RoutedEventHandler click)
    {
        var btn = new Button
        {
            Content = text,
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 6, 12, 6)
        };
        btn.Click += click;
        return btn;
    }
}
