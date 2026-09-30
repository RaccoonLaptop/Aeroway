using System.Net.Http;
using System.Net.Http.Headers;
using ZapretUI.Helpers;

namespace ZapretUI.Services;

public sealed record HostsCatalogSnapshot(
    IReadOnlyList<HostListSection> Sections,
    bool FromNetwork,
    int UpdatedBlocks);

public sealed class HostsCatalogService
{
    private const string MalwUrl = "https://raw.githubusercontent.com/ImMALWARE/dns.malw.link/master/hosts";

    private readonly UpdateService _updates;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly Dictionary<string, IReadOnlyList<string>> _lines = new(StringComparer.OrdinalIgnoreCase);

    public HostsCatalogService(UpdateService updates) => _updates = updates;

    public async Task<HostsCatalogSnapshot> RefreshAppliedAsync(CancellationToken ct = default)
    {
        var snapshot = await LoadAsync(ct).ConfigureAwait(false);
        var pending = snapshot.Sections
            .Where(section => SystemHostsFile.HasBlock(section.Id) && !SystemHostsFile.BlockMatches(section.Id, section.Lines))
            .ToList();
        for (var i = 0; i < pending.Count; i++)
            SystemHostsFile.SetBlock(pending[i].Id, pending[i].Lines, flushDns: i == pending.Count - 1);
        return snapshot with { UpdatedBlocks = pending.Count };
    }

    public async Task<HostsCatalogSnapshot> LoadAsync(CancellationToken ct = default)
    {
        var result = new List<HostListSection>();
        var fromNetwork = true;

        var flowsealRemote = false;
        string flowsealText;
        try
        {
            var flowsealDownload = await _updates.DownloadFlowsealHostsRemoteFirstAsync(ct).ConfigureAwait(false);
            flowsealText = flowsealDownload.Content;
            flowsealRemote = flowsealDownload.FromNetwork;
            if (flowsealRemote)
                SaveCache("flowseal.hosts", flowsealText);
        }
        catch
        {
            flowsealText = ReadCache("flowseal.hosts")
                ?? await _updates.DownloadFlowsealHostsAsync(ct).ConfigureAwait(false);
        }

        var flowseal = HostsListParser.ParseFlat(flowsealText);
        _lines["flowseal"] = flowseal;
        result.Add(new HostListSection("flowseal", "Flowseal", flowseal));

        string malwText;
        try
        {
            malwText = await DownloadMalwAsync(ct).ConfigureAwait(false);
            SaveCache("malw.hosts", malwText);
        }
        catch
        {
            fromNetwork = false;
            malwText = ReadCache("malw.hosts")
                ?? throw new InvalidOperationException("Список dns.malw.link не скачался и сохранённой копии нет.");
        }

        foreach (var section in HostsListParser.ParseSections(malwText, "malw", Loc.T("service.hosts_other")))
        {
            _lines[section.Id] = section.Lines;
            result.Add(section);
        }

        return new HostsCatalogSnapshot(result, fromNetwork, 0);
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

    private static string? ReadCache(string name)
    {
        var path = Path.Combine(CacheDir, name);
        if (!File.Exists(path))
            return null;
        var content = File.ReadAllText(path);
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }
}
