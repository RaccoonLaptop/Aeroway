using System.Net;
using System.Text.RegularExpressions;

namespace ZapretUI.Services;

public sealed record HostListSection(string Id, string Title, IReadOnlyList<string> Lines);

public static class HostsListParser
{
    public static IReadOnlyList<HostListSection> ParseSections(string content, string idPrefix, string preambleTitle = "Разное")
    {
        var sections = new List<(string Title, List<string> Lines)>();
        var title = preambleTitle;
        var lines = new List<string>();

        void Flush()
        {
            if (lines.Count == 0)
                return;
            sections.Add((title, lines.ToList()));
            lines = [];
        }

        foreach (var raw in content.Split(['\r', '\n']))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith('#'))
            {
                var header = line[1..].Trim();
                if (header.Length == 0 || IsIgnorableHeader(header))
                    continue;
                Flush();
                title = header;
                continue;
            }

            if (TryNormalize(line, out var entry))
                lines.Add(entry);
        }

        Flush();

        return sections
            .Select(section => new HostListSection(MakeId(idPrefix, section.Title), section.Title, section.Lines))
            .ToList();
    }

    public static IReadOnlyList<string> ParseFlat(string content)
    {
        var lines = new List<string>();
        foreach (var raw in content.Split(['\r', '\n']))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            if (TryNormalize(line, out var entry))
                lines.Add(entry);
        }

        return lines;
    }

    public static string MakeId(string prefix, string title)
    {
        var slug = Regex.Replace(title.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0)
            slug = "other";
        var id = prefix + "-" + slug;
        return id.Length <= 48 ? id : id[..48].Trim('-');
    }

    private static bool IsIgnorableHeader(string header)
    {
        var text = header.ToLowerInvariant();
        return text.Contains("обновлен", StringComparison.Ordinal)
               || text.Contains("update", StringComparison.Ordinal)
               || text.Contains("dns.malw", StringComparison.Ordinal)
               || text.Contains("hosts file", StringComparison.Ordinal);
    }

    private static bool TryNormalize(string line, out string entry)
    {
        entry = "";
        var comment = line.IndexOf('#');
        if (comment >= 0)
            line = line[..comment];
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !IPAddress.TryParse(parts[0], out var ip))
            return false;
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
            return false;

        for (var i = 1; i < parts.Length; i++)
        {
            if (!IsHost(parts[i]))
                return false;
        }

        entry = string.Join(' ', parts);
        return true;
    }

    private static bool IsHost(string host) =>
        host.Length > 0
        && host.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '_');
}
