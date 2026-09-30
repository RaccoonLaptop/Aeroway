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
    private const string MalwUrl = "https://raw.githubusercontent.com/ImMALWARE/dns.malw.link/master/hosts";

    private readonly UpdateService _updates;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly Dictionary<string, IReadOnlyList<string>> _lines = new(StringComparer.OrdinalIgnoreCase);

    public HostsCatalogService(UpdateService updates) => _updates = updates;

    public HostsCatalogSnapshot LoadLocal()
    {
        var flowsealCache = ReadCache("flowseal.hosts");
        var malwCache = ReadCache("malw.hosts");
        var flowseal = flowsealCache ?? ReadBundled("flowseal.hosts") ?? "";
        var malw = malwCache ?? ReadBundled("malw.hosts") ?? "";
        return Build(flowseal, malw, fromNetwork: false, builtIn: malwCache is null);
    }

    public async Task<HostsCatalogSnapshot> RefreshAppliedAsync(CancellationToken ct = default)
    {
        var snapshot = await TryLoadNetworkAsync(ct).ConfigureAwait(false) ?? LoadLocal();
        var pending = snapshot.Sections
            .Where(section => SystemHostsFile.HasBlock(section.Id) && !SystemHostsFile.BlockMatches(section.Id, section.Lines))
            .ToList();
        for (var i = 0; i < pending.Count; i++)
            SystemHostsFile.SetBlock(pending[i].Id, pending[i].Lines, flushDns: i == pending.Count - 1);
        return snapshot with { UpdatedBlocks = pending.Count };
    }

    public Task<HostsCatalogSnapshot> LoadAsync(CancellationToken ct = default) =>
        RefreshAppliedAsync(ct);

    private async Task<HostsCatalogSnapshot?> TryLoadNetworkAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var token = timeout.Token;

        var flowsealTask = DownloadFlowsealSafeAsync(token);
        var malwTask = DownloadMalwSafeAsync(token);
        await Task.WhenAll(flowsealTask, malwTask).ConfigureAwait(false);

        var malw = await malwTask.ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(malw))
            return null;

        SaveCache("malw.hosts", malw);
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

        return Build(flowseal, malw, fromNetwork: true, builtIn: false);
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

    private async Task<string?> DownloadMalwSafeAsync(CancellationToken ct)
    {
        try
        {
            return await DownloadMalwAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private HostsCatalogSnapshot Build(string flowsealText, string malwText, bool fromNetwork, bool builtIn)
    {
        var result = new List<HostListSection>();
        var flowseal = HostsListParser.ParseFlat(flowsealText);
        _lines["flowseal"] = flowseal;
        result.Add(new HostListSection("flowseal", "Flowseal", flowseal));

        foreach (var section in HostsListParser.ParseSections(malwText, "malw", Loc.T("service.hosts_other")))
        {
            _lines[section.Id] = section.Lines;
            result.Add(section);
        }

        return new HostsCatalogSnapshot(result, fromNetwork, 0, builtIn);
    }

    public IReadOnlyList<string> Lines(string id) =>
        _lines.TryGetValue(id, out var lines) ? lines : throw new InvalidOperationException("Список ещё не загружен.");

    private async Task<string> DownloadMalwAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, MalwUrl);
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
