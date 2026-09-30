using System.Diagnostics;
using System.Net;

namespace ZapretUI.Services;

public static class SystemHostsFile
{
    private static readonly object Gate = new();

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "drivers", "etc", "hosts");

    public static bool HasBlock(string id, string? path = null)
    {
        lock (Gate)
        {
            var file = path ?? DefaultPath;
            if (!File.Exists(file))
                return false;
            return File.ReadLines(file).Any(line => line.Trim() == Begin(id));
        }
    }

    public static bool BlockMatches(string id, IReadOnlyList<string> entries, string? path = null)
    {
        lock (Gate)
        {
            var found = ReadBlock(id, path);
            if (found is null)
                return entries.Count == 0;
            if (found.Count != entries.Count)
                return false;
            for (var i = 0; i < found.Count; i++)
            {
                if (!string.Equals(found[i], entries[i].Trim(), StringComparison.Ordinal))
                    return false;
            }

            return true;
        }
    }

    public static bool Covers(string id, IReadOnlyList<string> entries, string? path = null)
    {
        lock (Gate)
        {
            if (ReadBlock(id, path) is not null)
                return true;
            var file = path ?? DefaultPath;
            if (!File.Exists(file) || entries.Count == 0)
                return false;
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadLines(file))
            {
                foreach (var host in HostsInLine(line))
                    present.Add(host);
            }

            return HostsIn(entries).All(present.Contains);
        }
    }

    public static void RemoveEntries(string id, IReadOnlyList<string> entries, string? path = null)
    {
        lock (Gate)
        {
            RequireId(id);
            var file = path ?? DefaultPath;
            var lines = File.Exists(file) ? File.ReadAllLines(file).ToList() : [];
            lines = RemoveBlock(lines, id);
            lines = StripHosts(lines, HostsIn(entries));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllLines(file, lines);
            if (path is null)
                FlushDns();
        }
    }

    public static void SetBlock(string id, IReadOnlyList<string> entries, string? path = null, bool flushDns = true)
    {
        lock (Gate)
        {
            RequireId(id);
            var file = path ?? DefaultPath;
            var lines = File.Exists(file)
                ? File.ReadAllLines(file).ToList()
                : [];
            lines = RemoveBlock(lines, id);
            if (entries.Count > 0)
            {
                var hosts = HostsIn(entries);
                lines = StripHosts(lines, hosts);
                if (lines.Count > 0 && lines[^1].Length > 0)
                    lines.Add("");
                lines.Add(Begin(id));
                lines.AddRange(entries);
                lines.Add(End(id));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllLines(file, lines);
            if (path is null && flushDns)
                FlushDns();
        }
    }

    public static void RemoveBlock(string id, string? path = null) =>
        SetBlock(id, [], path);

    private static List<string> RemoveBlock(List<string> lines, string id)
    {
        var result = new List<string>();
        var skipping = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed == Begin(id))
            {
                skipping = true;
                continue;
            }

            if (skipping)
            {
                if (trimmed == End(id))
                    skipping = false;
                continue;
            }

            result.Add(line);
        }

        return result;
    }

    private static List<string> StripHosts(List<string> lines, HashSet<string> hosts)
    {
        var result = new List<string>();
        var inside = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# Aeroway:", StringComparison.Ordinal) && !trimmed.EndsWith(":end", StringComparison.Ordinal))
                inside = true;
            else if (trimmed.StartsWith("# Aeroway:", StringComparison.Ordinal) && trimmed.EndsWith(":end", StringComparison.Ordinal))
            {
                inside = false;
                result.Add(line);
                continue;
            }

            if (!inside && HostsInLine(trimmed).Any(hosts.Contains))
                continue;
            result.Add(line);
        }

        return result;
    }

    private static HashSet<string> HostsIn(IReadOnlyList<string> entries)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            foreach (var host in HostsInLine(entry))
                hosts.Add(host);
        }

        return hosts;
    }

    private static IEnumerable<string> HostsInLine(string line)
    {
        var comment = line.IndexOf('#');
        if (comment >= 0)
            line = line[..comment];
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !IPAddress.TryParse(parts[0], out _))
            yield break;
        for (var i = 1; i < parts.Length; i++)
            yield return parts[i];
    }

    private static void FlushDns()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("ipconfig", "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
            process?.WaitForExit(4000);
        }
        catch
        {
            /* записи уже в файле, кэш обновится сам */
        }
    }

    private static void RequireId(string id)
    {
        if (id.Length is 0 or > 48 || id.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch != '-'))
            throw new ArgumentException("Некорректный список hosts.", nameof(id));
    }

    private static List<string>? ReadBlock(string id, string? path)
    {
        var file = path ?? DefaultPath;
        if (!File.Exists(file))
            return null;
        List<string>? found = null;
        foreach (var line in File.ReadLines(file))
        {
            var trimmed = line.Trim();
            if (found is null)
            {
                if (trimmed == Begin(id))
                    found = [];
                continue;
            }

            if (trimmed == End(id))
                return found;
            if (trimmed.Length > 0)
                found.Add(trimmed);
        }

        return found;
    }

    private static string Begin(string id) => $"# Aeroway:{id}";

    private static string End(string id) => $"# Aeroway:{id}:end";
}
