using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace ZapretUI.Services;

public sealed class UpdateService
{
    private const string VersionUrl = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/main/.service/version.txt";
    private const string ReleaseUrl = "https://github.com/Flowseal/zapret-discord-youtube/releases/latest";
    private static readonly string[] HostsUrls =
    [
        "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/hosts",
        "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/main/.service/hosts"
    ];

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly ZapretPaths _paths;

    public UpdateService(ZapretPaths paths) => _paths = paths;

    public string LocalVersion => _paths.GetLocalVersion();

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, VersionUrl);
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var remote = ZapretPaths.NormalizeVersionText(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var local = LocalVersion;
            return new UpdateCheckResult
            {
                LocalVersion = local,
                RemoteVersion = remote,
                IsUpToDate = string.Equals(local, remote, StringComparison.OrdinalIgnoreCase),
                ReleaseUrl = ReleaseUrl
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult { Error = ex.Message, LocalVersion = LocalVersion };
        }
    }

    public async Task<bool> UpdateIpsetListAsync(CancellationToken ct = default)
    {
        var url = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/ipset-service.txt";
        var listFile = Path.Combine(_paths.Lists, "ipset-all.txt");
        var content = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
        Directory.CreateDirectory(_paths.Lists);
        await File.WriteAllTextAsync(listFile, content, ct).ConfigureAwait(false);
        return true;
    }

    public Task<string> DownloadFlowsealHostsAsync(CancellationToken ct = default) =>
        GetReferenceHostsContentAsync(ct);

    public async Task<(string Content, bool FromNetwork)> DownloadFlowsealHostsRemoteFirstAsync(CancellationToken ct = default)
    {
        var remote = await TryDownloadRemoteHostsAsync(ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(remote))
            return (remote, true);
        return (await GetReferenceHostsContentAsync(ct).ConfigureAwait(false), false);
    }

    public async Task<HostsUpdateResult> PrepareHostsUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var content = await GetReferenceHostsContentAsync(ct).ConfigureAwait(false);
            var lines = ParseHostsLines(content);
            if (lines.Count == 0)
                return new HostsUpdateResult { Error = "Hosts reference file is empty." };

            var systemHostsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "drivers", "etc", "hosts");

            var systemLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(systemHostsPath))
            {
                foreach (var line in await File.ReadAllLinesAsync(systemHostsPath, ct).ConfigureAwait(false))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length > 0)
                        systemLines.Add(trimmed);
                }
            }

            var needsUpdate = !systemLines.Contains(lines[0]) || !systemLines.Contains(lines[^1]);
            var tempFile = Path.Combine(Path.GetTempPath(), "zapret_hosts.txt");
            await File.WriteAllTextAsync(tempFile, content, ct).ConfigureAwait(false);

            if (!needsUpdate)
            {
                TryDeleteFile(tempFile);
                return new HostsUpdateResult { IsUpToDate = true };
            }

            return new HostsUpdateResult
            {
                NeedsManualMerge = true,
                TempFilePath = tempFile,
                SystemHostsPath = systemHostsPath
            };
        }
        catch (Exception ex)
        {
            return new HostsUpdateResult { Error = ex.Message };
        }
    }

    public static void OpenHostsMergeAssist(string tempFilePath, string systemHostsPath)
    {
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{tempFilePath}\"")
        {
            UseShellExecute = true
        });
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{systemHostsPath}\"")
        {
            UseShellExecute = true
        });
    }

    private async Task<string> GetReferenceHostsContentAsync(CancellationToken ct)
    {
        foreach (var path in GetLocalHostsFallbackPaths())
        {
            if (!File.Exists(path))
                continue;

            var content = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(content))
                return content;
        }

        var remote = await TryDownloadRemoteHostsAsync(ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(remote))
            return remote;

        throw new InvalidOperationException("Failed to download hosts file and no local fallback is available.");
    }

    private async Task<string?> TryDownloadRemoteHostsAsync(CancellationToken ct)
    {
        foreach (var url in HostsUrls)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Aeroway", "1.0"));
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    continue;

                var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(content))
                    return content;
            }
            catch
            {
                // Try next source.
            }
        }

        return null;
    }

    private IEnumerable<string> GetLocalHostsFallbackPaths()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "zapret", ".service", "hosts"),
            Path.Combine(BundledZapretService.BundledDirectory, ".service", "hosts"),
            Path.Combine(AppContext.BaseDirectory, "packaging", "zapret", ".service", "hosts"),
            Path.Combine(_paths.Root, ".service", "hosts"),
            Path.Combine(ZapretPaths.GetBundledZapretPath(), ".service", "hosts"),
        };

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> ParseHostsLines(string content) =>
        content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    public void OpenReleasePage() =>
        Process.Start(new ProcessStartInfo(ReleaseUrl) { UseShellExecute = true });

    public string GetReleaseUrl() => ReleaseUrl;
}

public sealed class UpdateCheckResult
{
    public string LocalVersion { get; init; } = "";
    public string RemoteVersion { get; init; } = "";
    public bool IsUpToDate { get; init; }
    public string ReleaseUrl { get; init; } = "";
    public string? Error { get; init; }
}

public sealed class HostsUpdateResult
{
    public bool IsUpToDate { get; init; }
    public bool NeedsManualMerge { get; init; }
    public string? TempFilePath { get; init; }
    public string? SystemHostsPath { get; init; }
    public string? Error { get; init; }
}
