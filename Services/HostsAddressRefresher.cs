using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ZapretUI.Services;

public sealed record HostsAddressRefreshResult(string Content, int Resolved, int Kept);

public static class HostsAddressRefresher
{
    private const string Preamble = "\u0001";
    private static readonly HttpClient Doh = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static bool HasDuplicateHosts(string content)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var host in Hosts(content))
        {
            if (!seen.Add(host))
                return true;
        }

        return false;
    }

    public static Task<HostsAddressRefreshResult> RefreshAsync(string content, CancellationToken ct = default) =>
        RefreshAsync(content, ResolveLowestAAsync, ct);

    public static async Task<HostsAddressRefreshResult> RefreshAsync(
        string content,
        Func<string, CancellationToken, Task<IPAddress?>> resolve,
        CancellationToken ct = default)
    {
        var sections = new List<(string Title, List<(string Host, string Fallback)> Rows)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in HostsListParser.ParseSections(content, "geohide", Preamble))
        {
            var rows = new List<(string Host, string Fallback)>();
            foreach (var line in section.Lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;
                for (var i = 1; i < parts.Length; i++)
                {
                    if (!seen.Add(parts[i]))
                        continue;
                    rows.Add((parts[i], parts[0]));
                }
            }

            if (rows.Count > 0)
                sections.Add((section.Title, rows));
        }

        var chosen = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var resolved = 0;
        var kept = 0;
        using var gate = new SemaphoreSlim(12);
        var tasks = new List<Task>();
        foreach (var row in sections.SelectMany(section => section.Rows))
        {
            tasks.Add(Task.Run(async () =>
            {
                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var address = await resolve(row.Host, ct).ConfigureAwait(false);
                    if (address is null)
                    {
                        chosen[row.Host] = row.Fallback;
                        Interlocked.Increment(ref kept);
                    }
                    else
                    {
                        chosen[row.Host] = address.ToString();
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

        var text = new StringBuilder();
        text.AppendLine("### geohide.ru: hosts file");
        text.AppendLine("# Обновлено Aeroway: один A-адрес на домен");
        foreach (var section in sections)
        {
            text.AppendLine();
            if (section.Title != Preamble)
                text.Append("# ").AppendLine(section.Title);
            foreach (var row in section.Rows)
                text.Append(chosen[row.Host]).Append(' ').AppendLine(row.Host);
        }

        return new HostsAddressRefreshResult(text.ToString().TrimEnd() + Environment.NewLine, resolved, kept);
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

    public static async Task<IPAddress?> ResolveLowestAAsync(string host, CancellationToken ct)
    {
        var ascii = ToAscii(host);
        if (ascii.Length == 0)
            return null;

        var cloudflare = await QueryDohAsync("https://cloudflare-dns.com/dns-query", ascii, ct).ConfigureAwait(false);
        if (cloudflare.Definitive)
            return cloudflare.Address;
        var google = await QueryDohAsync("https://dns.google/resolve", ascii, ct).ConfigureAwait(false);
        return google.Address;
    }

    private static async Task<(IPAddress? Address, bool Definitive)> QueryDohAsync(string endpoint, string host, CancellationToken ct)
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
                return (null, false);
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("Status", out var status))
                return (null, false);
            var code = status.GetInt32();
            if (code == 3)
                return (null, true);
            if (code != 0)
                return (null, false);

            IPAddress? best = null;
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
                    if (best is null || Compare(address, best) < 0)
                        best = address;
                }
            }

            return (best, true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return (null, false);
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

    private static int Compare(IPAddress left, IPAddress right)
    {
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
