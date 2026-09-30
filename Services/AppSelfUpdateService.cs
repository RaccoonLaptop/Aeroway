using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZapretUI.Helpers;

namespace ZapretUI.Services;

public sealed class AppSelfUpdateService
{
    // URL манифеста обновлений (можно переопределить в settings.json)
    public const string DefaultManifestUrl =
        "https://raw.githubusercontent.com/RaccoonLaptop/Aeroway/main/update.json";

    private const string GitHubReleasesApi =
        "https://api.github.com/repos/RaccoonLaptop/Aeroway/releases/latest";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private readonly AppSettings _settings;
    private readonly string _installDir;
    private readonly string? _zapretRoot;

    public AppSelfUpdateService(AppSettings settings, string? zapretRoot = null)
    {
        _settings = settings;
        _installDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        _zapretRoot = zapretRoot;
    }

    public static string GetLocalVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var plus = info.IndexOf('+');
            return plus > 0 ? info[..plus] : info;
        }
        return asm.GetName().Version?.ToString(3) ?? "1.0.0";
    }

    public async Task<AppUpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var local = GetLocalVersion();
        try
        {
            var manifest = await LoadManifestAsync(ct);
            if (manifest is null)
            {
                return new AppUpdateCheckResult
                {
                    LocalVersion = local,
                    IsUpToDate = true,
                    Message = "Манифест обновлений не найден"
                };
            }

            var remote = manifest.Version.Trim();
            var hasUpdate = AppVersionCompare.IsNewer(remote, local);
            return new AppUpdateCheckResult
            {
                LocalVersion = local,
                RemoteVersion = remote,
                IsUpToDate = !hasUpdate,
                HasUpdate = hasUpdate,
                Manifest = manifest,
                Message = hasUpdate
                    ? $"Доступна версия {remote}"
                    : $"Aeroway актуален ({local})"
            };
        }
        catch (Exception ex)
        {
            return new AppUpdateCheckResult
            {
                LocalVersion = local,
                IsUpToDate = true,
                Error = ex.Message
            };
        }
    }

    public async Task<AppUpdatePrepareResult> PrepareUpdateAsync(
        AppUpdateManifest manifest,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ZapretUI-update-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempRoot);

            if (!string.IsNullOrWhiteSpace(manifest.InstallerUrl))
            {
                var setupPath = Path.Combine(tempRoot, "Aeroway-Setup.exe");
                await DownloadFileWithProgressAsync(manifest.InstallerUrl.Trim(), setupPath, progress, ct);
                return AppUpdatePrepareResult.Ok(new PreparedAppUpdate
                {
                    TempRoot = tempRoot,
                    InstallerExePath = setupPath,
                    ManifestVersion = manifest.Version
                });
            }

            var extractDir = Path.Combine(tempRoot, "extract");
            Directory.CreateDirectory(extractDir);

            var packagePath = await ResolvePackagePathAsync(manifest, tempRoot, progress, ct);
            if (packagePath is null)
                return AppUpdatePrepareResult.Fail("Файл обновления не найден");

            if (packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report(new DownloadProgress { Phase = "Распаковка архива..." });
                ZipFile.ExtractToDirectory(packagePath, extractDir, overwriteFiles: true);
            }
            else if (Directory.Exists(packagePath))
            {
                CopyDirectory(packagePath, extractDir);
            }
            else
            {
                return AppUpdatePrepareResult.Fail("Неподдерживаемый формат пакета");
            }

            var sourceDir = FindProgramRoot(extractDir);
            if (sourceDir is null || !HasProgramExe(sourceDir))
                return AppUpdatePrepareResult.Fail("В пакете нет Aeroway.exe");

            return AppUpdatePrepareResult.Ok(new PreparedAppUpdate
            {
                TempRoot = tempRoot,
                SourceDir = sourceDir,
                ManifestVersion = manifest.Version
            });
        }
        catch (Exception ex)
        {
            try { Directory.Delete(tempRoot, true); } catch { /* ignore */ }
            return AppUpdatePrepareResult.Fail(ex.Message);
        }
    }

    public Task<AppUpdateInstallResult> InstallPreparedUpdateAsync(PreparedAppUpdate prepared, CancellationToken ct = default)
    {
        try
        {
            PrepareForSelfUpdateInstall();

            if (!string.IsNullOrWhiteSpace(prepared.InstallerExePath))
                return InstallViaInstallerAsync(prepared);

            var scriptPath = ResolveUpdateScript();
            if (scriptPath is null)
                return Task.FromResult(AppUpdateInstallResult.Fail("Скрипт apply-update.ps1 не найден"));

            var installPayloadDir = Path.Combine(
                Path.GetTempPath(),
                "ZapretUI-install-payload",
                prepared.ManifestVersion);
            if (Directory.Exists(installPayloadDir))
                Directory.Delete(installPayloadDir, true);
            CopyDirectory(prepared.SourceDir, installPayloadDir);

            var logFile = GetUpdateLogPath();
            var exePath = InstalledExePath();
            var pid = Process.GetCurrentProcess().Id;
            StartUpdateProgressUi(logFile, prepared.ManifestVersion);
            var args = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" " +
                       $"-SourceDir \"{installPayloadDir}\" -TargetDir \"{_installDir}\" -ProcessId {pid} " +
                       $"-ExePath \"{exePath}\" -LogFile \"{logFile}\" " +
                       $"-StagingDir \"{prepared.TempRoot}\"";

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (process is null)
                return Task.FromResult(AppUpdateInstallResult.Fail("Не удалось запустить установку обновления."));

            _settings.LastInstalledVersion = prepared.ManifestVersion;
            _settings.Save();
            return Task.FromResult(AppUpdateInstallResult.Ok(
                "Обновление запускается, программа перезапустится...",
                restart: true,
                keepPreparedFiles: true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AppUpdateInstallResult.Fail(ex.Message));
        }
    }

    public async Task<AppUpdateInstallResult> InstallUpdateAsync(AppUpdateManifest manifest, CancellationToken ct = default)
    {
        var prepared = await PrepareUpdateAsync(manifest, progress: null, ct);
        if (!prepared.Success || prepared.Payload is null)
            return AppUpdateInstallResult.Fail(prepared.Message);
        return await InstallPreparedUpdateAsync(prepared.Payload, ct);
    }

    public static void CleanupPreparedUpdate(PreparedAppUpdate? prepared, bool keepForInstall = false)
    {
        if (prepared is null || keepForInstall) return;
        try
        {
            if (Directory.Exists(prepared.TempRoot))
                Directory.Delete(prepared.TempRoot, true);
        }
        catch { /* ignore */ }
    }

    public async Task<AppUpdateInstallResult> CheckAndInstallIfNeededAsync(bool autoInstall, CancellationToken ct = default)
    {
        var check = await CheckForUpdateAsync(ct);
        if (check.Error is not null)
            return AppUpdateInstallResult.Fail(check.Error);

        if (!check.HasUpdate || check.Manifest is null)
            return AppUpdateInstallResult.Ok(check.Message ?? "Обновление не требуется");

        if (!Helpers.UiHelpers.Confirm(
                $"Доступна новая версия Aeroway: {check.RemoteVersion} (у вас {check.LocalVersion}).\n\nУстановить сейчас?"))
        {
            return AppUpdateInstallResult.Ok("Обновление отложено");
        }

        return await InstallUpdateAsync(check.Manifest, ct);
    }

    private async Task<AppUpdateManifest?> LoadManifestAsync(CancellationToken ct)
    {
        var candidates = new List<AppUpdateManifest>();

        foreach (var url in GetRemoteManifestUrls())
        {
            var manifest = await TryLoadManifestUrlAsync(url, ct);
            if (manifest is not null)
                candidates.Add(manifest);
        }

        var fromRelease = await TryLoadManifestFromGitHubReleaseAsync(ct);
        if (fromRelease is not null)
            candidates.Add(fromRelease);

        if (candidates.Count > 0)
            return PickNewestManifest(candidates);

        foreach (var path in GetLocalManifestPaths())
        {
            var manifest = await TryLoadManifestFileAsync(path, ct);
            if (manifest is not null)
                return manifest;
        }

        return null;
    }

    private IEnumerable<string> GetRemoteManifestUrls()
    {
        if (!string.IsNullOrWhiteSpace(_settings.UpdateManifestUrl))
            yield return _settings.UpdateManifestUrl.Trim();

        yield return DefaultManifestUrl;
    }

    private IEnumerable<string> GetLocalManifestPaths()
    {
        yield return Path.Combine(_installDir, "update.json");

        if (!string.IsNullOrEmpty(_zapretRoot))
            yield return Path.Combine(_zapretRoot, "ZapretUI-update", "update.json");
    }

    private async Task<AppUpdateManifest?> TryLoadManifestUrlAsync(string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            req.Headers.TryAddWithoutValidation("Pragma", "no-cache");
            var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;
            var json = await res.Content.ReadAsStringAsync(ct);
            return ParseManifest(json, "");
        }
        catch
        {
            return null;
        }
    }

    private async Task<AppUpdateManifest?> TryLoadManifestFileAsync(string path, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = await File.ReadAllTextAsync(path, ct);
            return ParseManifest(json, Path.GetDirectoryName(path)!);
        }
        catch
        {
            return null;
        }
    }

    private async Task<AppUpdateManifest?> TryLoadManifestFromGitHubReleaseAsync(CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, GitHubReleasesApi);
            req.Headers.UserAgent.ParseAdd("ZapretUI-Updater");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString()?.Trim().TrimStart('v') ?? "";
            if (string.IsNullOrEmpty(tag)) return null;

            string? setupUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    var url = asset.GetProperty("browser_download_url").GetString();
                    if (string.IsNullOrEmpty(url)) continue;
                    if (name.Equals("Aeroway-Setup.exe", StringComparison.OrdinalIgnoreCase))
                        setupUrl = url;
                }
            }

            return new AppUpdateManifest
            {
                Version = tag,
                InstallerUrl = setupUrl,
                BaseDirectory = ""
            };
        }
        catch
        {
            return null;
        }
    }

    private static AppUpdateManifest? ParseManifest(string json, string baseDirectory)
    {
        var manifest = JsonSerializer.Deserialize<AppUpdateManifest>(json, JsonOptions);
        if (manifest is null) return null;
        manifest.BaseDirectory = baseDirectory;
        return manifest;
    }

    private static AppUpdateManifest PickNewestManifest(IReadOnlyList<AppUpdateManifest> manifests)
    {
        AppUpdateManifest? best = null;
        Version? bestVersion = null;

        foreach (var m in manifests)
        {
            if (!Version.TryParse(AppVersionCompare.Normalize(m.Version), out var v))
                continue;
            if (bestVersion is null || v > bestVersion)
            {
                bestVersion = v;
                best = m;
            }
        }

        return best ?? manifests[0];
    }

    private async Task<string?> ResolvePackagePathAsync(
        AppUpdateManifest manifest,
        string tempRoot,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(manifest.PackageFile))
        {
            var local = Path.IsPathRooted(manifest.PackageFile)
                ? manifest.PackageFile
                : Path.Combine(manifest.BaseDirectory, manifest.PackageFile);

            if (File.Exists(local))
            {
                var dest = Path.Combine(tempRoot, Path.GetFileName(local));
                File.Copy(local, dest, true);
                return dest;
            }
        }

        if (!string.IsNullOrWhiteSpace(manifest.DownloadUrl))
        {
            var url = manifest.DownloadUrl.Trim();
            if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var path = new Uri(url).LocalPath;
                return File.Exists(path) ? path : null;
            }

            var zipPath = Path.Combine(tempRoot, "package.zip");
            await DownloadFileWithProgressAsync(url, zipPath, progress, ct);
            return zipPath;
        }

        return null;
    }

    private static string? FindProgramRoot(string extractDir)
    {
        if (HasProgramExe(extractDir))
            return extractDir;

        foreach (var dir in Directory.GetDirectories(extractDir))
        {
            if (HasProgramExe(dir))
                return dir;
        }
        return null;
    }

    private static bool HasProgramExe(string directory) =>
        File.Exists(Path.Combine(directory, "Aeroway.exe"))
        || File.Exists(Path.Combine(directory, "ZapretUI.exe"));

    public static string GetInstallConfirmMessage(string remoteVersion)
    {
        var message = LocalizationService.F("update.app_ready", remoteVersion);
        if (ZapretShutdownService.IsWinDivertOrWinwsActive())
            message += "\n\n" + LocalizationService.T("update.bypass_active_hint");
        return message;
    }

    private static void PrepareForSelfUpdateInstall()
    {
        try
        {
            ZapretShutdownService.StopAll();
            Thread.Sleep(400);
        }
        catch { /* updater script will retry stopping bypass */ }
    }

    private static string? ResolveUpdateScript()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Scripts", "apply-update.ps1"),
            Path.Combine(AppContext.BaseDirectory, "apply-update.ps1")
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private static string? ResolveInstallerUpdateScript()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Scripts", "apply-update-installer.ps1"),
            Path.Combine(AppContext.BaseDirectory, "apply-update-installer.ps1")
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private Task<AppUpdateInstallResult> InstallViaInstallerAsync(PreparedAppUpdate prepared)
    {
        var scriptPath = ResolveInstallerUpdateScript();
        if (scriptPath is null)
            return Task.FromResult(AppUpdateInstallResult.Fail("Скрипт apply-update-installer.ps1 не найден"));

        var logFile = GetUpdateLogPath();
        var exePath = InstalledExePath();
        var pid = Process.GetCurrentProcess().Id;
        StartUpdateProgressUi(logFile, prepared.ManifestVersion);
        var args = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" " +
                   $"-InstallerPath \"{prepared.InstallerExePath}\" -TargetDir \"{_installDir}\" -ProcessId {pid} " +
                   $"-ExePath \"{exePath}\" -LogFile \"{logFile}\" " +
                   $"-StagingDir \"{prepared.TempRoot}\"";

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        if (process is null)
            return Task.FromResult(AppUpdateInstallResult.Fail("Не удалось запустить установку обновления."));

        _settings.LastInstalledVersion = prepared.ManifestVersion;
        _settings.Save();
        return Task.FromResult(AppUpdateInstallResult.Ok(
            "Загружен установщик. Программа закроется и обновится...",
            restart: true,
            keepPreparedFiles: true));
    }

    private string InstalledExePath() =>
        InstallDirMigration.FirstExistingExe(_installDir)
        ?? Path.Combine(_installDir, "ZapretUI.exe");

    private static string GetUpdateLogPath() =>
        Path.Combine(Path.GetTempPath(), "ZapretUI-update.log");

    private static void StartUpdateProgressUi(string logFile, string version)
    {
        UpdateProgressLauncher.Start(logFile, version);
        Thread.Sleep(450);
    }

    private async Task DownloadFileWithProgressAsync(
        string url,
        string destinationPath,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        progress?.Report(new DownloadProgress
        {
            Phase = "Загрузка обновления...",
            BytesReceived = 0,
            TotalBytes = totalBytes,
            BytesPerSecond = 0
        });

        await using var network = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(destinationPath);

        var buffer = new byte[1024 * 128];
        long received = 0;
        var stopwatch = Stopwatch.StartNew();
        var lastReport = stopwatch.Elapsed;
        long bytesAtLastReport = 0;

        while (true)
        {
            var read = await network.ReadAsync(buffer, ct);
            if (read == 0) break;

            await file.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;

            if (stopwatch.Elapsed - lastReport >= TimeSpan.FromMilliseconds(250))
            {
                var elapsed = (stopwatch.Elapsed - lastReport).TotalSeconds;
                var speed = elapsed > 0 ? (received - bytesAtLastReport) / elapsed : 0;
                progress?.Report(new DownloadProgress
                {
                    Phase = "Загрузка обновления...",
                    BytesReceived = received,
                    TotalBytes = totalBytes,
                    BytesPerSecond = speed
                });
                lastReport = stopwatch.Elapsed;
                bytesAtLastReport = received;
            }
        }

        progress?.Report(new DownloadProgress
        {
            Phase = "Загрузка завершена",
            BytesReceived = received,
            TotalBytes = totalBytes ?? received,
            BytesPerSecond = 0
        });
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };
}

public sealed class AppUpdateManifest
{
    public string Version { get; set; } = "";
    public string? DownloadUrl { get; set; }
    public string? InstallerUrl { get; set; }
    public string? PackageFile { get; set; }

    [JsonIgnore] public string BaseDirectory { get; set; } = "";
}

public sealed class AppUpdateCheckResult
{
    public string LocalVersion { get; init; } = "";
    public string RemoteVersion { get; init; } = "";
    public bool IsUpToDate { get; init; }
    public bool HasUpdate { get; init; }
    public AppUpdateManifest? Manifest { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
}

public sealed class AppUpdateInstallResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public bool RequiresRestart { get; init; }
    public bool KeepPreparedFiles { get; init; }

    public static AppUpdateInstallResult Ok(string msg, bool restart = false, bool keepPreparedFiles = false) =>
        new() { Success = true, Message = msg, RequiresRestart = restart, KeepPreparedFiles = keepPreparedFiles };

    public static AppUpdateInstallResult Fail(string msg) =>
        new() { Success = false, Message = msg };
}

public sealed class AppUpdatePrepareResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public PreparedAppUpdate? Payload { get; init; }

    public static AppUpdatePrepareResult Ok(PreparedAppUpdate payload) =>
        new() { Success = true, Message = "Пакет обновления загружен", Payload = payload };

    public static AppUpdatePrepareResult Fail(string msg) =>
        new() { Success = false, Message = msg };
}

public sealed class PreparedAppUpdate
{
    public string TempRoot { get; init; } = "";
    public string SourceDir { get; init; } = "";
    public string? InstallerExePath { get; init; }
    public string ManifestVersion { get; init; } = "";
}
