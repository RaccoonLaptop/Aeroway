using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aeroway.Models;

namespace Aeroway.Helpers;

public static class TestTargetTableRenderer
{
    public static void Render(StackPanel host, IReadOnlyList<TestTargetRow> targets)
    {
        host.Children.Clear();
        if (targets.Count == 0)
            return;

        var rows = TestTargetRowFormatter.DedupeByKey(targets);
        var table = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 8, 0),
            UseLayoutRounding = true
        };
        Grid.SetIsSharedSizeScope(table, true);
        table.ColumnDefinitions.Add(AutoCol("Name"));
        table.ColumnDefinitions.Add(AutoCol("Http"));
        table.ColumnDefinitions.Add(AutoCol("Tls12"));
        table.ColumnDefinitions.Add(AutoCol("Tls13"));
        table.ColumnDefinitions.Add(AutoCol("PingLabel"));
        table.ColumnDefinitions.Add(AutoCol("PingValue"));

        for (var i = 0; i < rows.Count; i++)
        {
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddRow(table, rows[i], i);
        }

        host.Children.Add(table);
    }

    private static ColumnDefinition AutoCol(string group) => new()
    {
        Width = GridLength.Auto,
        SharedSizeGroup = group
    };

    private static void AddRow(Grid table, TestTargetRow row, int rowIndex)
    {
        AddCell(table, row.Name, TestTableLineFormatter.TextBrush(), rowIndex, 0, trailingPad: 10);

        if (row.PingOnly)
        {
            AddCell(table, " | " + TestTargetRowFormatter.FormatPingLabel(),
                TestTableLineFormatter.MutedBrush(), rowIndex, 4);
            AddCell(table, TestTargetRowFormatter.FormatPingValue(row.Ping),
                TestTargetRowFormatter.PingBrush(row.Ping), rowIndex, 5);
            return;
        }

        AddToken(table, row.Http, rowIndex, 1);
        AddToken(table, row.Tls12, rowIndex, 2);
        AddToken(table, row.Tls13, rowIndex, 3);

        if (IsPingHidden(row.Ping))
            return;

        AddCell(table, " | " + TestTargetRowFormatter.FormatPingLabel(),
            TestTableLineFormatter.MutedBrush(), rowIndex, 4);
        AddCell(table, TestTargetRowFormatter.FormatPingValue(row.Ping),
            TestTargetRowFormatter.PingBrush(row.Ping), rowIndex, 5);
    }

    private static void AddToken(Grid table, string rawToken, int row, int column) =>
        AddCell(table, " | " + TestTargetRowFormatter.FormatProtocolToken(rawToken),
            TestTargetRowFormatter.TokenBrush(rawToken), row, column);

    private static void AddCell(Grid table, string text, Brush foreground, int row, int column, double trailingPad = 0)
    {
        var visible = text.Replace(' ', '\u00A0');
        var block = new TextBlock
        {
            Text = visible,
            FontFamily = TerminalFonts.Mono,
            FontSize = TerminalFonts.Size,
            Foreground = foreground,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, trailingPad, 4)
        };
        TerminalFonts.ApplyDisplayMode(block);
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        table.Children.Add(block);
    }

    private static bool IsPingHidden(string? ping) =>
        string.IsNullOrWhiteSpace(ping)
        || ping is "n/a" or "—"
        || ping.Equals("n/a", StringComparison.OrdinalIgnoreCase);
}
