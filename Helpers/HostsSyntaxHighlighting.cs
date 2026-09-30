using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace ZapretUI.Helpers;

public static class HostsSyntaxHighlighting
{
    private static IHighlightingDefinition? _cached;

    public static void Apply(TextEditor editor)
    {
        editor.SyntaxHighlighting = GetDefinition();
        editor.FontFamily = new System.Windows.Media.FontFamily("Consolas");
        editor.FontSize = 15;
        editor.ShowLineNumbers = true;
        editor.WordWrap = false;
        editor.IsReadOnly = true;
        editor.Background = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("InputBrush");
        editor.Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("TextBrush");
        editor.LineNumbersForeground = System.Windows.Media.Brushes.Gray;
        editor.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
        editor.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        MouseWheelScrollHelper.Attach(editor);
    }

    private static IHighlightingDefinition GetDefinition()
    {
        if (_cached is not null)
            return _cached;

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Hosts.xshd");
        using var reader = XmlReader.Create(path);
        _cached = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        return _cached;
    }
}
