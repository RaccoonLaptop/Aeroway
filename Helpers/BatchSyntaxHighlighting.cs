using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace Aeroway.Helpers;

public static class BatchSyntaxHighlighting
{
    private static IHighlightingDefinition? _cached;

    public static void Apply(TextEditor editor)
    {
        editor.SyntaxHighlighting = GetDefinition();
        editor.FontSize = 15;
        editor.LineNumbersForeground = System.Windows.Media.Brushes.Gray;
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        MouseWheelScrollHelper.Attach(editor);
    }

    private static IHighlightingDefinition GetDefinition()
    {
        if (_cached is not null) return _cached;

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "BatchZapret.xshd");
        if (File.Exists(path))
        {
            using var reader = XmlReader.Create(path);
            _cached = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            return _cached;
        }

        _cached = HighlightingManager.Instance.GetDefinition("PowerShell")
                  ?? HighlightingManager.Instance.GetDefinition("JavaScript")
                  ?? HighlightingManager.Instance.GetDefinition("XML")
                  ?? HighlightingManager.Instance.GetDefinition("Default");
        return _cached;
    }
}
