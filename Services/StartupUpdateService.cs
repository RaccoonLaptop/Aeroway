using System.IO;
using System.Windows;
using Aeroway;
using Aeroway.Helpers;

namespace Aeroway.Services;

public sealed class StartupUpdateService
{
    public async Task<bool> CheckAndPromptAsync(Window owner, AppSettings settings, ZapretPaths paths)
    {
        if (!settings.CheckUpdatesOnStartup)
            return false;

        var appUpdater = new AppSelfUpdateService(settings, paths.Root);
        var flowUpdater = new UpdateService(paths);

        var appCheckTask = appUpdater.CheckForUpdateAsync();
        var flowCheckTask = flowUpdater.CheckForUpdatesAsync();
        await Task.WhenAll(appCheckTask, flowCheckTask);

        var appCheck = await appCheckTask;
        var flowCheck = await flowCheckTask;

        ConsoleLog.Instance.Write(
            Loc.F("startup.log_check", appCheck.LocalVersion, flowCheck.LocalVersion));

        if (appCheck.Error is not null)
            ConsoleLog.Instance.Write($"Aeroway: {appCheck.Error}");
        if (flowCheck.Error is not null)
            ConsoleLog.Instance.Write($"Flowseal: {flowCheck.Error}");

        var appUpdateAvailable = appCheck.HasUpdate && appCheck.Manifest is not null;
        var flowUpdateAvailable = flowCheck.Error is null && !flowCheck.IsUpToDate;

        if (!appUpdateAvailable && !flowUpdateAvailable)
        {
            ConsoleLog.Instance.Write(Loc.T("startup.all_up_to_date"));
            return false;
        }

        if (appUpdateAvailable)
        {
            if (UiHelpers.Confirm(
                    Loc.F("update.app_startup", appCheck.RemoteVersion, appCheck.LocalVersion),
                    owner))
            {
                PreparedAppUpdate? prepared = null;
                var keepPrepared = false;
                UpdateDownloadWindow? progressWin = new UpdateDownloadWindow(Loc.T("update.download_title"), Loc.T("update.download_status"));
                progressWin.Owner = owner;
                progressWin.Show();
                try
                {
                    var downloadProgress = new Progress<DownloadProgress>(p => progressWin.ReportProgress(p));
                    var preparedResult = await appUpdater.PrepareUpdateAsync(
                        appCheck.Manifest!, downloadProgress);
                    if (!preparedResult.Success || preparedResult.Payload is null)
                    {
                        UiHelpers.ShowError(preparedResult.Message);
                    }
                    else
                    {
                        prepared = preparedResult.Payload;
                        progressWin.SetStatus(Loc.T("update.download_complete"));
                        if (UiHelpers.Confirm(
                                AppSelfUpdateService.GetInstallConfirmMessage(appCheck.RemoteVersion),
                                owner))
                        {
                            progressWin.SetStatus(Loc.T("update.starting_install"));
                            progressWin.ReportProgress(new DownloadProgress
                            {
                                Phase = Loc.T("update.install_phase")
                            });
                            var install = await appUpdater.InstallPreparedUpdateAsync(prepared);
                            progressWin.Close();
                            progressWin = null!;
                            ConsoleLog.Instance.Write(install.Message);
                            keepPrepared = install.KeepPreparedFiles;
                            if (install.RequiresRestart)
                                return true;
                            if (!install.Success)
                                UiHelpers.ShowError(install.Message);
                        }
                    }
                }
                finally
                {
                    progressWin?.Close();
                    AppSelfUpdateService.CleanupPreparedUpdate(prepared, keepPrepared);
                }
            }
        }

        if (flowUpdateAvailable)
        {
            if (UiHelpers.Confirm(
                    Loc.F("update.flowseal_startup", flowCheck.RemoteVersion, flowCheck.LocalVersion),
                    owner))
            {
                await FlowsealReinstallService.ReinstallAsync(owner, paths);
            }
        }

        return false;
    }
}

public static class FlowsealReinstallService
{
    public static async Task<bool> ReinstallAsync(Window? owner, ZapretPaths paths)
    {
        var target = paths.Root;
        string? installedStrategy = null;
        FlowsealUserDataBackup? backup = null;

        try
        {
            try
            {
                var strategySvc = new StrategyService(paths, new ProcessRunner());
                installedStrategy = await strategySvc.GetServiceStrategyAsync();
                if (string.IsNullOrWhiteSpace(installedStrategy))
                    installedStrategy = null;
            }
            catch { /* ignore */ }

            backup = FlowsealUserDataBackup.Create(target);
            ZapretShutdownService.StopAll();
            await Task.Delay(800);

            if (Directory.Exists(target))
                Directory.Delete(target, true);

            var bootstrap = new BootstrapWindow(target) { Owner = owner };
            if (bootstrap.ShowDialog() != true)
            {
                backup.TryRestore(target);
                UiHelpers.ShowResult(owner, "Flowseal", Loc.T("update.flowseal_cancel"));
                return false;
            }

            backup.TryRestore(target);
            BundledStrategiesService.DeployTo(target);
            backup.RestoreCustomStrategies(target);

            if (installedStrategy is not null)
            {
                var batName = installedStrategy.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                    ? installedStrategy
                    : installedStrategy + ".bat";
                var batPath = Path.Combine(target, batName);
                if (File.Exists(batPath) &&
                    UiHelpers.Confirm(Loc.F("update.flowseal_restore_service", installedStrategy), owner))
                {
                    var runner = new ProcessRunner();
                    runner.SetZapretRoot(target);
                    await runner.RunBridgeAsync("InstallService", batName);
                }
            }

            var done = Loc.T("update.flowseal_done_restart");
            if (!string.IsNullOrWhiteSpace(backup.Warning))
                done += Environment.NewLine + Environment.NewLine + backup.Warning;
            UiHelpers.ShowResult(owner, "Flowseal", done);
            return true;
        }
        catch (Exception ex)
        {
            backup?.TryRestore(target);
            UiHelpers.ShowResult(owner, "Flowseal", $"{Loc.T("common.error_prefix")} {ex.Message}");
            return false;
        }
        finally
        {
            backup?.Dispose();
        }
    }
}

internal sealed class FlowsealUserDataBackup : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _warnings = [];

    public string Warning => string.Join(Environment.NewLine, _warnings);

    private FlowsealUserDataBackup(string tempDir) => _tempDir = tempDir;

    public static FlowsealUserDataBackup? Create(string zapretRoot)
    {
        if (!Directory.Exists(zapretRoot))
            return null;

        var tempDir = Path.Combine(Path.GetTempPath(), "zapretui-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var backup = new FlowsealUserDataBackup(tempDir);

        backup.CopyListsDirIfExists(Path.Combine(zapretRoot, "lists"), tempDir, "lists");
        CopyUtilsFileIfExists(Path.Combine(zapretRoot, "utils", "game_filter.enabled"),
            Path.Combine(tempDir, "utils"), "game_filter.enabled");
        CopyUtilsFileIfExists(Path.Combine(zapretRoot, "utils", "check_updates.enabled"),
            Path.Combine(tempDir, "utils"), "check_updates.enabled");
        backup.CopyCustomBats(zapretRoot);

        return backup;
    }

    public void RestoreCustomStrategies(string zapretRoot)
    {
        var batsBackup = Path.Combine(_tempDir, "bats");
        if (!Directory.Exists(batsBackup) || !Directory.Exists(zapretRoot))
            return;

        foreach (var file in Directory.GetFiles(batsBackup, "*.bat"))
        {
            var name = Path.GetFileName(file);
            var dest = Path.Combine(zapretRoot, name);
            if (File.Exists(dest))
                continue;

            try
            {
                File.Copy(file, dest, false);
            }
            catch (Exception ex)
            {
                _warnings.Add(name + ": " + ex.Message);
            }
        }
    }

    private void CopyCustomBats(string zapretRoot)
    {
        var destDir = Path.Combine(_tempDir, "bats");
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(zapretRoot, "*.bat"))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith("service", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                File.Copy(file, Path.Combine(destDir, name), true);
            }
            catch (Exception ex)
            {
                _warnings.Add(name + ": " + ex.Message);
            }
        }
    }

    public void TryRestore(string zapretRoot)
    {
        if (!Directory.Exists(_tempDir) || !Directory.Exists(zapretRoot))
            return;

        var listsBackup = Path.Combine(_tempDir, "lists");
        if (Directory.Exists(listsBackup))
        {
            var listsTarget = Path.Combine(zapretRoot, "lists");
            Directory.CreateDirectory(listsTarget);
            foreach (var file in Directory.GetFiles(listsBackup, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(listsBackup, file);
                if (!ShouldRestoreListFile(rel))
                    continue;

                var dest = Path.Combine(listsTarget, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                try { File.Copy(file, dest, true); }
                catch (Exception ex) { _warnings.Add(rel + ": " + ex.Message); }
            }
        }

        RestoreUtilsFile(zapretRoot, "game_filter.enabled");
        RestoreUtilsFile(zapretRoot, "check_updates.enabled");
        RestoreCustomStrategies(zapretRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { /* ignore */ }
    }

    private static bool ShouldRestoreListFile(string relativePath)
    {
        var name = Path.GetFileName(relativePath);
        if (name.EndsWith("-user.txt", StringComparison.OrdinalIgnoreCase))
            return true;
        return name.Equals("ipset-all.txt.backup", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyUtilsFileIfExists(string sourceFile, string destDir, string destName)
    {
        if (!File.Exists(sourceFile))
            return;

        Directory.CreateDirectory(destDir);
        File.Copy(sourceFile, Path.Combine(destDir, destName), true);
    }

    private void CopyListsDirIfExists(string sourceDir, string destRoot, string destSubDir)
    {
        if (!Directory.Exists(sourceDir))
            return;

        var destDir = Path.Combine(destRoot, destSubDir);
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            if (!ShouldRestoreListFile(rel))
                continue;

            var dest = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            try { File.Copy(file, dest, true); }
            catch (Exception ex) { _warnings.Add(rel + ": " + ex.Message); }
        }
    }

    private void RestoreUtilsFile(string zapretRoot, string fileName)
    {
        var source = Path.Combine(_tempDir, "utils", fileName);
        if (!File.Exists(source))
            return;

        var utilsDir = Path.Combine(zapretRoot, "utils");
        Directory.CreateDirectory(utilsDir);
        try { File.Copy(source, Path.Combine(utilsDir, fileName), true); }
        catch (Exception ex) { _warnings.Add(fileName + ": " + ex.Message); }
    }
}
