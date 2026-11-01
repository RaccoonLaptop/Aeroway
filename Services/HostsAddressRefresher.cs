using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Aeroway.Services;

public sealed record HostsAddressRefreshResult(string Content, int Resolved, int Kept);

public sealed record HostLinesRefresh(IReadOnlyList<string> Lines, int Resolved, int Kept);

public static class HostsAddressRefresher
{
    private const string Preamble = "\u0001";
    private static readonly HttpClient Doh = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static readonly string[] WorkingAddresses =
    [
        "193.233.112.88",
        "193.233.112.68",
        "159.194.200.33",
        "193.233.112.67"
    ];

    public static bool HasDuplicateHosts(string content)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in HostsListParser.ParseSections(content, "geohide", Preamble))
        {
            foreach (var line in section.Lines)
            {
                if (!seen.Add(line))
                    return true;
            }
        }

        return false;
    }

    public static bool IsLegacyList(string content) =>
        content.Contains("один A-адрес", StringComparison.Ordinal)
        || content.Contains("все A-записи", StringComparison.Ordinal);

    public static HostLinesRefresh ApplyWorkingAddresses(IReadOnlyList<string> lines)
    {
        var hosts = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 1; i < parts.Length; i++)
            {
                if (seen.Add(parts[i]))
                    hosts.Add(parts[i]);
            }
        }

        var rewritten = new List<string>(hosts.Count * WorkingAddresses.Length);
        foreach (var host in hosts)
        {
            foreach (var address in WorkingAddresses)
                rewritten.Add(address + " " + host);
        }

        return new HostLinesRefresh(rewritten, hosts.Count, 0);
    }

    public static string ApplyWorkingAddresses(string content)
    {
        var text = new StringBuilder();
        text.AppendLine("### geohide.ru: hosts file");
        text.AppendLine("# Обновлено Aeroway: рабочие адреса GeoHide на каждый домен");
        foreach (var section in HostsListParser.ParseSections(content, "geohide", Preamble))
        {
            text.AppendLine();
            if (section.Title != Preamble)
                text.Append("# ").AppendLine(section.Title);
            var lines = section.Title.Contains("без прокси", StringComparison.OrdinalIgnoreCase)
                ? section.Lines
                : ApplyWorkingAddresses(section.Lines).Lines;
            foreach (var line in lines)
                text.AppendLine(line);
        }

        return text.ToString().TrimEnd() + Environment.NewLine;
    }

    public static Task<HostsAddressRefreshResult> RefreshAsync(string content, CancellationToken ct = default) =>
        RefreshAsync(content, ResolveAAsync, ct);

    public static async Task<HostsAddressRefreshResult> RefreshAsync(
        string content,
        Func<string, CancellationToken, Task<IReadOnlyList<IPAddress>>> resolve,
        CancellationToken ct = default)
    {
        var sections = CollectSections(content);
        var (chosen, resolved, kept) = await ResolveRowsAsync(sections.SelectMany(section => section.Rows), resolve, ct).ConfigureAwait(false);

        var text = new StringBuilder();
        text.AppendLine("### geohide.ru: hosts file");
        text.AppendLine("# Обновлено Aeroway: все A-записи на домен");
        foreach (var section in sections)
        {
            text.AppendLine();
            if (section.Title != Preamble)
                text.Append("# ").AppendLine(section.Title);
            foreach (var row in section.Rows)
            {
                foreach (var address in chosen[row.Host])
                    text.Append(address).Append(' ').AppendLine(row.Host);
            }
        }

        return new HostsAddressRefreshResult(text.ToString().TrimEnd() + Environment.NewLine, resolved, kept);
    }

    public static Task<HostLinesRefresh> RefreshLinesAsync(IReadOnlyList<string> lines, CancellationToken ct = default) =>
        RefreshLinesAsync(lines, ResolveAAsync, ct);

    public static async Task<HostLinesRefresh> RefreshLinesAsync(
        IReadOnlyList<string> lines,
        Func<string, CancellationToken, Task<IReadOnlyList<IPAddress>>> resolve,
        CancellationToken ct = default)
    {
        var rows = CollectRows(lines, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var (chosen, resolved, kept) = await ResolveRowsAsync(rows, resolve, ct).ConfigureAwait(false);
        var rewritten = new List<string>();
        foreach (var row in rows)
        {
            foreach (var address in chosen[row.Host])
                rewritten.Add(address + " " + row.Host);
        }

        return new HostLinesRefresh(rewritten, resolved, kept);
    }

    public static string ReplaceNamedSection(string content, string title, IReadOnlyList<string> lines)
    {
        var rows = content.Replace("\r\n", "\n").Split('\n').ToList();
        var start = rows.FindIndex(line => line.Trim() == "# " + title);
        if (start < 0)
            throw new InvalidOperationException("Раздел не найден: " + title);

        var end = start + 1;
        while (end < rows.Count && !rows[end].TrimStart().StartsWith('#'))
            end++;

        rows.RemoveRange(start + 1, end - start - 1);
        var insert = new List<string>(lines);
        if (start + 1 < rows.Count)
            insert.Add("");
        rows.InsertRange(start + 1, insert);
        return string.Join('\n', rows).TrimEnd() + "\n";
    }

    private static List<(string Title, List<(string Host, IReadOnlyList<string> Fallback)> Rows)> CollectSections(string content)
    {
        var sections = new List<(string Title, List<(string Host, IReadOnlyList<string> Fallback)> Rows)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in HostsListParser.ParseSections(content, "geohide", Preamble))
        {
            var rows = CollectRows(section.Lines, seen);
            if (rows.Count > 0)
                sections.Add((section.Title, rows));
        }

        return sections;
    }

    private static List<(string Host, IReadOnlyList<string> Fallback)> CollectRows(IReadOnlyList<string> lines, HashSet<string> seen)
    {
        var order = new List<string>();
        var fallbacks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;
            for (var i = 1; i < parts.Length; i++)
            {
                var host = parts[i];
                if (fallbacks.TryGetValue(host, out var addresses))
                {
                    if (!addresses.Contains(parts[0], StringComparer.Ordinal))
                        addresses.Add(parts[0]);
                    continue;
                }

                if (!seen.Add(host))
                    continue;
                fallbacks[host] = [parts[0]];
                order.Add(host);
            }
        }

        return order.Select(host => (host, (IReadOnlyList<string>)fallbacks[host])).ToList();
    }

    private static async Task<(System.Collections.Concurrent.ConcurrentDictionary<string, string[]> Chosen, int Resolved, int Kept)> ResolveRowsAsync(
        IEnumerable<(string Host, IReadOnlyList<string> Fallback)> rows,
        Func<string, CancellationToken, Task<IReadOnlyList<IPAddress>>> resolve,
        CancellationToken ct)
    {
        var chosen = new System.Collections.Concurrent.ConcurrentDictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var resolved = 0;
        var kept = 0;
        using var gate = new SemaphoreSlim(12);
        var tasks = new List<Task>();
        foreach (var row in rows)
        {
            tasks.Add(Task.Run(async () =>
            {
                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var addresses = await resolve(row.Host, ct).ConfigureAwait(false);
                    if (addresses.Count == 0)
                    {
                        chosen[row.Host] = SortAddresses(row.Fallback.Select(IPAddress.Parse));
                        Interlocked.Increment(ref kept);
                    }
                    else
                    {
                        chosen[row.Host] = SortAddresses(addresses);
                        Interlocked.Increment(ref resolved);
                    }
                }
                finally
                {
                    gate.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return (chosen, resolved, kept);
    }

    public static string KeepExtraHosts(string refreshed, string previous)
    {
        var present = new HashSet<string>(Hosts(refreshed), StringComparer.OrdinalIgnoreCase);
        var extras = new List<(string Title, string Line)>();
        foreach (var section in HostsListParser.ParseSections(previous, "geohide", Preamble))
        {
            foreach (var line in section.Lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || parts.Skip(1).All(present.Contains))
                    continue;
                extras.Add((section.Title, line));
                foreach (var host in parts.Skip(1))
                    present.Add(host);
            }
        }

        if (extras.Count == 0)
            return refreshed;

        var lines = refreshed.Replace("\r\n", "\n").TrimEnd().Split('\n').ToList();
        foreach (var group in extras.GroupBy(item => item.Title))
        {
            var header = group.Key == Preamble ? -1 : lines.FindIndex(line => line.Trim() == "# " + group.Key);
            if (header < 0)
            {
                lines.Add("");
                if (group.Key != Preamble)
                    lines.Add("# " + group.Key);
                lines.AddRange(group.Select(item => item.Line));
                continue;
            }

            var insertAt = header + 1;
            while (insertAt < lines.Count && !lines[insertAt].StartsWith("# ", StringComparison.Ordinal))
                insertAt++;
            lines.InsertRange(insertAt, group.Select(item => item.Line));
        }

        return string.Join('\n', lines).TrimEnd() + "\n";
    }

    public static async Task<IReadOnlyList<IPAddress>> ResolveAAsync(string host, CancellationToken ct)
    {
        var ascii = ToAscii(host);
        if (ascii.Length == 0)
            return [];

        var cloudflare = await QueryDohAsync("https://cloudflare-dns.com/dns-query", ascii, ct).ConfigureAwait(false);
        if (cloudflare.Definitive)
            return cloudflare.Addresses;
        var google = await QueryDohAsync("https://dns.google/resolve", ascii, ct).ConfigureAwait(false);
        return google.Addresses;
    }

    private static async Task<(IReadOnlyList<IPAddress> Addresses, bool Definitive)> QueryDohAsync(string endpoint, string host, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                endpoint + "?name=" + Uri.EscapeDataString(host) + "&type=A");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dns-json"));
            using var response = await Doh.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return ([], false);
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("Status", out var status))
                return ([], false);
            var code = status.GetInt32();
            if (code == 3)
                return ([], true);
            if (code != 0)
                return ([], false);

            var found = new List<IPAddress>();
            if (document.RootElement.TryGetProperty("Answer", out var answers))
            {
                foreach (var answer in answers.EnumerateArray())
                {
                    if (!answer.TryGetProperty("type", out var type) || type.GetInt32() != 1)
                        continue;
                    if (!answer.TryGetProperty("data", out var data))
                        continue;
                    if (!IPAddress.TryParse(data.GetString(), out var address) || address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    if (!found.Contains(address))
                        found.Add(address);
                }
            }

            return (found, true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return ([], false);
        }
    }

    private static string[] SortAddresses(IEnumerable<IPAddress> addresses) =>
        addresses.Distinct().OrderBy(address => address, IpOrder.Instance).Select(address => address.ToString()).ToArray();

    private sealed class IpOrder : IComparer<IPAddress>
    {
        public static readonly IpOrder Instance = new();

        public int Compare(IPAddress? left, IPAddress? right)
        {
            if (left is null || right is null)
                return 0;
            var a = left.GetAddressBytes();
            var b = right.GetAddressBytes();
            var length = Math.Min(a.Length, b.Length);
            for (var i = 0; i < length; i++)
            {
                var order = a[i].CompareTo(b[i]);
                if (order != 0)
                    return order;
            }

            return a.Length.CompareTo(b.Length);
        }
    }

    private static string ToAscii(string host)
    {
        try
        {
            return new System.Globalization.IdnMapping().GetAscii(host);
        }
        catch (ArgumentException)
        {
            return "";
        }
    }

    private static IEnumerable<string> Hosts(string content)
    {
        foreach (var section in HostsListParser.ParseSections(content, "geohide", Preamble))
        {
            foreach (var line in section.Lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (var i = 1; i < parts.Length; i++)
                    yield return parts[i];
            }
        }
    }

}
