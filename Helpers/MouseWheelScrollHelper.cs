using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;using ICSharpCode.AvalonEdit;

namespace ZapretUI.Helpers;

public static class MouseWheelScrollHelper
{
    public static void Attach(TextBox textBox)
    {
        textBox.PreviewMouseWheel += (_, e) =>
        {
            ScrollTextBoxBase(textBox, e);
            e.Handled = true;
        };
    }

    public static void Attach(RichTextBox richTextBox)
    {
        richTextBox.PreviewMouseWheel += (_, e) =>
        {
            if (ScrollRichTextBox(richTextBox, e))
                e.Handled = true;
        };
    }

    public static void Attach(TextEditor editor)
    {
        editor.PreviewMouseWheel += (_, e) =>
        {
            if (ScrollTextEditor(editor, e))
                e.Handled = true;
        };
    }

    private static bool ScrollTextEditor(TextEditor editor, MouseWheelEventArgs e)
    {
        var max = Math.Max(0, editor.ExtentHeight - editor.ViewportHeight);
        if (max <= 0)
            return false;

        var lineHeight = editor.TextArea.TextView.DefaultLineHeight;
        if (lineHeight < 1)
            lineHeight = editor.FontSize + 3;

        var linesPerNotch = SystemParameters.WheelScrollLines;
        if (linesPerNotch < 1)
            linesPerNotch = 3;

        var distance = Math.Abs(e.Delta) / 120.0 * linesPerNotch * lineHeight;
        var next = editor.VerticalOffset + (e.Delta > 0 ? -distance : distance);
        if (next < 0)
            next = 0;
        else if (next > max)
            next = max;

        if (Math.Abs(next - editor.VerticalOffset) < 0.5)
            return false;

        editor.ScrollToVerticalOffset(next);
        return true;
    }

    public static void Attach(ListBox listBox)
    {
        listBox.PreviewMouseWheel += (_, e) =>
        {
            var scrollViewer = FindScrollViewer(listBox);
            if (scrollViewer is null) return;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        };
    }

    public static void Attach(ScrollViewer scrollViewer)
    {
        scrollViewer.PreviewMouseWheel += (_, e) =>
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        };
    }

    private static void ScrollTextBoxBase(TextBoxBase textBox, MouseWheelEventArgs e)
    {
        var steps = Math.Max(1, Math.Abs(e.Delta) / 120);
        for (var i = 0; i < steps; i++)
        {
            if (e.Delta > 0)
                textBox.LineUp();
            else
                textBox.LineDown();
        }
    }

    private static bool ScrollRichTextBox(RichTextBox richTextBox, MouseWheelEventArgs e)
    {
        var scrollViewer = FindScrollViewer(richTextBox);
        if (scrollViewer is not null)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            return true;
        }

        ScrollTextBoxBase(richTextBox, e);
        return true;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)    {
        if (root is ScrollViewer sv)
            return sv;

        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            var found = FindScrollViewer(child);
            if (found is not null)
                return found;
        }

        return null;
    }
}
