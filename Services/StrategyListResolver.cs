using System.IO;
using System.Text.RegularExpressions;

namespace Aeroway.Services;

/// <summary>
/// Извлекает имена list-файлов из содержимого .bat стратегии.
/// </summary>
public static class StrategyListResolver
{
    private static readonly Regex ListsPathRegex = new(
        @"(?:%LISTS%|lists[/\\])([a-zA-Z0-9_\-\.]+\.txt)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ArgValueRegex = new(
        @"(?:--hostlist(?:-exclude)?|--ipset(?:-exclude)?)\s*=\s*(""[^""]*""|'[^']*'|[^\s^]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed record ResolvedList(string FileName, bool ExistsOnDisk, bool ReferencedInBat);

    public static IReadOnlyList<ResolvedList> Resolve(string batContent, ZapretPaths paths)
    {
        var normalized = NormalizeBatContent(batContent);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in ListsPathRegex.Matches(normalized))
            AddName(names, m.Groups[1].Value);

        foreach (Match m in ArgValueRegex.Matches(normalized))
        {
            var raw = m.Groups[1].Value.Trim().Trim('"', '\'');
            AddName(names, raw);
        }

        var listsDir = paths.Lists;
        var onDisk = Directory.Exists(listsDir)
            ? Directory.GetFiles(listsDir, "*.txt")
                .Select(Path.GetFileName)
                .Where(f => f is not null && !f.EndsWith(".backup", StringComparison.OrdinalIgnoreCase))
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = new List<ResolvedList>();
        foreach (var name in SortListNames(names))
        {
            result.Add(new ResolvedList(
                name,
                ExistsOnDisk: onDisk.Contains(name),
                ReferencedInBat: true));
        }

        return result;
    }

    private static string NormalizeBatContent(string batContent)
    {
        return batContent
            .Replace("^\r\n", " ", StringComparison.Ordinal)
            .Replace("^\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }

    private static void AddName(HashSet<string> names, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;

        var s = raw.Replace("%LISTS%", "", StringComparison.OrdinalIgnoreCase)
            .Replace("lists\\", "", StringComparison.OrdinalIgnoreCase)
            .Replace("lists/", "", StringComparison.OrdinalIgnoreCase)
            .Trim('"', '\'', ' ');

        var fileName = Path.GetFileName(s);
        if (string.IsNullOrEmpty(fileName)) return;
        if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) return;

        names.Add(fileName);
    }

    private static IEnumerable<string> SortListNames(IEnumerable<string> names)
    {
        string[] priority =
        [
            "list-general.txt",
            "list-general-user.txt",
            "list-google.txt",
            "list-exclude.txt",
            "list-exclude-user.txt",
            "ipset-all.txt",
            "ipset-exclude.txt",
            "ipset-exclude-user.txt"
        ];

        var list = names.ToList();
        var ordered = new List<string>();
        foreach (var p in priority)
        {
            var hit = list.FirstOrDefault(f => f.Equals(p, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                ordered.Add(hit);
                list.Remove(hit);
            }
        }

        ordered.AddRange(list.OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
        return ordered;
    }
}
