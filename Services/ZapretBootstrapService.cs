using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZapretUI.Services;

public sealed class ZapretBootstrapService
{
    private const string ReleasesApi =
        "https://api.github.com/repos/Flowseal/zapret-discord-youtube/releases/latest";

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromMinutes(15)
    };

    public ZapretBootstrapService()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Aeroway", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<BootstrapResult> EnsureInstalledAsync(
        string targetDir,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(targetDir);
            if (ZapretPaths.IsValidZapretRoot(targetDir))
            {
                return BootstrapResult.Ok("Компоненты zapret уже установлены.", targetDir);
            }

            progress?.Report("Получение информации о последней версии...");
            var release = await GetLatestReleaseAsync(ct);
            var asset = release.Assets.FirstOrDefault(a =>
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                ?? release.Assets.FirstOrDefault(a =>
                    a.Name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase));
            if (asset is null || string.IsNullOrWhiteSpace(asset.DownloadUrl))
                return BootstrapResult.Fail("Не найден архив (.zip/.rar) в последнем релизе Flowseal.");

            var tempRoot = Path.Combine(Path.GetTempPath(), "zapret-bootstrap-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var zipPath = Path.Combine(tempRoot, asset.Name);
                progress?.Report($"Скачивание {asset.Name} ({release.TagName})...");
                await DownloadFileAsync(asset.DownloadUrl, zipPath, progress, ct);

                var extractDir = Path.Combine(tempRoot, "extract");
                progress?.Report("Распаковка...");
                if (asset.Name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
                {
                    if (!ExtractRarArchive(zipPath, extractDir, progress))
                        return BootstrapResult.Fail("Не удалось распаковать .rar. Установите 7-Zip.");
                }
                else
                {
                    ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);
                }

                var packageRoot = ZapretPaths.ResolvePackageRoot(extractDir);
                if (packageRoot is null)
                    return BootstrapResult.Fail("Скачанный пакет повреждён или неполный (service.bat / bin не найдены).");

                foreach (var file in Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    var rel = Path.GetRelativePath(packageRoot, file);
                    var dest = Path.Combine(targetDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest, true);
                }

                if (!ZapretPaths.IsValidZapretRoot(targetDir))
                    return BootstrapResult.Fail("Скачанный пакет повреждён или неполный.");

                BundledStrategiesService.DeployTo(targetDir);
                try
                {
                    await new ServiceDefaultsService(new ZapretPaths(targetDir))
                        .ApplyFreshInstallDefaultsAsync(ct)
                        .ConfigureAwait(false);
                }
                catch { /* ignore */ }
                return BootstrapResult.Ok($"Установлено: Flowseal {release.TagName}", targetDir);
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { /* ignore */ }
            }
        }
        catch (OperationCanceledException)
        {
            return BootstrapResult.Fail("Загрузка отменена.");
        }
        catch (Exception ex)
        {
            if (BundledZapretService.TryDeployTo(targetDir, out var bundledMessage))
            {
                try
                {
                    await new ServiceDefaultsService(new ZapretPaths(targetDir))
                        .ApplyFreshInstallDefaultsAsync(ct)
                        .ConfigureAwait(false);
                }
                catch { /* ignore */ }

                return BootstrapResult.Ok(
                    $"{bundledMessage} (GitHub недоступен: {ex.Message})",
                    targetDir);
            }

            return BootstrapResult.Fail(ex.Message);
        }
    }

    private static bool ExtractRarArchive(string rarPath, string destDir, IProgress<string>? progress)
    {
        Directory.CreateDirectory(destDir);
        var sevenZipCandidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe")
        };
        var sevenZip = sevenZipCandidates.FirstOrDefault(File.Exists);
        if (sevenZip is null)
            return false;

        progress?.Report("Распаковка RAR через 7-Zip...");
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = sevenZip,
            Arguments = $"x \"{rarPath}\" -o\"{destDir}\" -y",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc is null) return false;
        proc.WaitForExit();
        return proc.ExitCode == 0;
    }

    private async Task DownloadFileAsync(
        string url,
        string destPath,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = File.Create(destPath);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            readTotal += read;
            if (total > 0)
            {
                var pct = (int)(readTotal * 100 / total);
                progress?.Report($"Скачивание... {pct}%");
            }
        }
    }

    private async Task<GitHubRelease> GetLatestReleaseAsync(CancellationToken ct)
    {
        var json = await _http.GetStringAsync(ReleasesApi, ct);
        var release = JsonSerializer.Deserialize<GitHubRelease>(json, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid GitHub API response.");
        if (string.IsNullOrWhiteSpace(release.TagName))
            throw new InvalidOperationException("Release tag not found.");
        return release;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; } = "";
    }
}

public sealed class BootstrapResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string? InstallPath { get; init; }

    public static BootstrapResult Ok(string message, string path) =>
        new() { Success = true, Message = message, InstallPath = path };

    public static BootstrapResult Fail(string message) =>
        new() { Success = false, Message = message };
}
