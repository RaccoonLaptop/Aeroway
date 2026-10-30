using System.Net.Http;
using System.Net.Http.Headers;
using ZapretUI.Helpers;

namespace ZapretUI.Services;

public sealed record HostsCatalogSnapshot(
    IReadOnlyList<HostListSection> Sections,
    bool FromNetwork,
    int UpdatedBlocks,
    bool BuiltIn = false);

public sealed class HostsCatalogService
{
    private const string GeoHideUrl = "https://raw.githubusercontent.com/Internet-Helper/GeoHideDNS/refs/heads/main/hosts/hosts";
    private const string GeoHideCache = "geohide.hosts";
    private const string LegacyMalwCache = "malw.hosts";

    private readonly UpdateService _updates;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly Dictionary<string, IReadOnlyList<string>> _lines = new(StringComparer.OrdinalIgnoreCase);

    public HostsCatalogService(UpdateService updates) => _updates = updates;

    public HostsCatalogSnapshot LoadLocal()
    {
        ForgetLegacyMalw();
        var flowsealCache = ReadCache("flowseal.hosts");
        var geoCache = ReadCache(GeoHideCache);
        if (geoCache is not null && HostsAddressRefresher.IsLegacyList(geoCache))
        {
            try { File.Delete(Path.Combine(CacheDir, GeoHideCache)); }
            catch { /* the bundled list is used instead */ }
            geoCache = null;
        }

        var flowseal = flowsealCache ?? ReadBundled("flowseal.hosts") ?? "";
        var geo = geoCache ?? ReadBundled(GeoHideCache) ?? "";
        return Build(flowseal, geo, fromNetwork: false, builtIn: geoCache is null);
    }

    public async Task<HostsCatalogSnapshot> RefreshAppliedAsync(CancellationToken ct = default, bool collapseAddresses = false)
    {
        ForgetLegacyMalw();
        var snapshot = await TryLoadNetworkAsync(ct, collapseAddresses).ConfigureAwait(false) ?? LoadLocal();
        return ApplyChangedBlocks(snapshot);
    }

    public async Task<(HostsCatalogSnapshot Snapshot, int Resolved, int Kept)> RefreshSectionAddressesAsync(HostListSection section, CancellationToken ct = default)
    {
        ForgetLegacyMalw();
        var refresh = await HostsAddressRefresher.RefreshLinesAsync(section.Lines, ct).ConfigureAwait(false);
        if (section.Id.Equals("flowseal", StringComparison.OrdinalIgnoreCase))
        {
            SaveCache("flowseal.hosts", string.Join('\n', refresh.Lines) + "\n");
        }
        else
        {
            var geo = ReadGeoHide() ?? "";
            SaveCache(GeoHideCache, HostsAddressRefresher.ReplaceNamedSection(geo, section.Title, refresh.Lines));
        }

        var flowseal = ReadCache("flowseal.hosts") ?? ReadBundled("flowseal.hosts") ?? "";
        var geoText = ReadGeoHide() ?? "";
        var snapshot = ApplyChangedBlocks(Build(flowseal, geoText, fromNetwork: true, builtIn: false));
        return (snapshot, refresh.Resolved, refresh.Kept);
    }

    public async Task<(HostsCatalogSnapshot Snapshot, HostsAddressRefreshResult Refresh)> RefreshAddressesAsync(CancellationToken ct = default)
    {
        ForgetLegacyMalw();
        var geo = ReadGeoHide() ?? "";
        var refresh = await HostsAddressRefresher.RefreshAsync(geo, ct).ConfigureAwait(false);
        SaveCache(GeoHideCache, refresh.Content);
        var flowseal = ReadCache("flowseal.hosts") ?? ReadBundled("flowseal.hosts") ?? "";
        var snapshot = ApplyChangedBlocks(Build(flowseal, refresh.Content, fromNetwork: true, builtIn: false));
        return (snapshot, refresh);
    }

    public Task<HostsCatalogSnapshot> LoadAsync(CancellationToken ct = default) =>
        RefreshAppliedAsync(ct);

    private async Task<HostsCatalogSnapshot?> TryLoadNetworkAsync(CancellationToken ct, bool collapseAddresses)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var token = timeout.Token;

        var flowsealTask = DownloadFlowsealSafeAsync(token);
        var geoTask = DownloadGeoHideSafeAsync(token);
        await Task.WhenAll(flowsealTask, geoTask).ConfigureAwait(false);

        var downloaded = await geoTask.ConfigureAwait(false);
        var local = ReadGeoHide();
        string? geo;
        if (collapseAddresses)
        {
            var source = string.IsNullOrWhiteSpace(downloaded) ? local : downloaded;
            if (string.IsNullOrWhiteSpace(source))
                return null;
            var refresh = await HostsAddressRefresher.RefreshAsync(source, ct).ConfigureAwait(false);
            geo = string.IsNullOrWhiteSpace(local)
                ? refresh.Content
                : HostsAddressRefresher.KeepExtraHosts(refresh.Content, local);
            SaveCache(GeoHideCache, geo);
        }
        else
        {
            geo = local;
            if (string.IsNullOrWhiteSpace(geo))
                return null;
        }

        var flowsealDownload = await flowsealTask.ConfigureAwait(false);
        string flowseal;
        if (flowsealDownload is not null)
        {
            flowseal = flowsealDownload;
            SaveCache("flowseal.hosts", flowseal);
        }
        else
        {
            flowseal = ReadCache("flowseal.hosts") ?? ReadBundled("flowseal.hosts") ?? "";
        }

        return Build(flowseal, geo, fromNetwork: true, builtIn: false);
    }

    private HostsCatalogSnapshot ApplyChangedBlocks(HostsCatalogSnapshot snapshot)
    {
        var pending = snapshot.Sections
            .Where(section => SystemHostsFile.HasBlock(section.Id) && !SystemHostsFile.BlockMatches(section.Id, section.Lines))
            .ToList();
        for (var i = 0; i < pending.Count; i++)
            SystemHostsFile.SetBlock(pending[i].Id, pending[i].Lines, flushDns: i == pending.Count - 1);
        return snapshot with { UpdatedBlocks = pending.Count };
    }

    private async Task<string?> DownloadFlowsealSafeAsync(CancellationToken ct)
    {
        try
        {
            var download = await _updates.DownloadFlowsealHostsRemoteFirstAsync(ct).ConfigureAwait(false);
            return download.FromNetwork ? download.Content : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> DownloadGeoHideSafeAsync(CancellationToken ct)
    {
        try
        {
            return await DownloadGeoHideAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private HostsCatalogSnapshot Build(string flowsealText, string geoText, bool fromNetwork, bool builtIn)
    {
        var result = new List<HostListSection>();
        var flowseal = HostsListParser.ParseFlat(flowsealText);
        _lines["flowseal"] = flowseal;
        result.Add(new HostListSection("flowseal", "Flowseal", flowseal));

        foreach (var section in HostsListParser.ParseSections(geoText, "geohide", Loc.T("service.hosts_other")))
        {
            _lines[section.Id] = section.Lines;
            result.Add(section);
        }

        return new HostsCatalogSnapshot(result, fromNetwork, 0, builtIn);
    }

    public IReadOnlyList<string> Lines(string id) =>
        _lines.TryGetValue(id, out var lines) ? lines : throw new InvalidOperationException("Список ещё не загружен.");

    private static void ForgetLegacyMalw()
    {
        var legacy = Path.Combine(CacheDir, LegacyMalwCache);
        if (File.Exists(legacy))
        {
            try { File.Delete(legacy); }
            catch { /* cache stays until the next launch */ }
        }

        try { SystemHostsFile.RemoveBlocksByPrefix("malw-"); }
        catch { /* hosts file is locked; the lists are simply no longer offered */ }
    }

    private async Task<string> DownloadGeoHideAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GeoHideUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Aeroway", "1.0"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private static string CacheDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        InstallDirMigration.FolderName,
        "hosts-cache");

    private static void SaveCache(string name, string content)
    {
        Directory.CreateDirectory(CacheDir);
        File.WriteAllText(Path.Combine(CacheDir, name), content);
    }

    private static string? ReadGeoHide()
    {
        var cache = ReadCache(GeoHideCache);
        if (cache is not null && !HostsAddressRefresher.IsLegacyList(cache))
            return cache;
        return ReadBundled(GeoHideCache);
    }

    private static string? ReadCache(string name) => ReadTextFile(Path.Combine(CacheDir, name));

    private static string? ReadBundled(string name) =>
        ReadTextFile(Path.Combine(AppContext.BaseDirectory, "Assets", "hosts", name));

    private static string? ReadTextFile(string path)
    {
        if (!File.Exists(path))
            return null;
        var content = File.ReadAllText(path);
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }
}
