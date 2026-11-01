using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Aeroway.Helpers;

namespace Aeroway.Services;

public sealed class StrategyService
{
    private readonly ZapretPaths _paths;
    private readonly ProcessRunner _runner;
    private string? _lastStartedStrategy;

    public StrategyService(ZapretPaths paths, ProcessRunner runner)
    {
        _paths = paths;
        _runner = runner;
    }

    public bool IsRunning() => Process.GetProcessesByName("winws").Length > 0;

    public string? GetRunningStrategyTitle()
    {
        foreach (var p in Process.GetProcessesByName("winws"))
        {
            try
            {
                var title = p.MainWindowTitle;
                if (title.StartsWith("zapret:", StringComparison.OrdinalIgnoreCase))
                    return title["zapret:".Length..].Trim();
            }
            catch { /* ignore */ }
        }

        return IsRunning() ? (_lastStartedStrategy ?? TryGetServiceStrategyName()) : null;
    }

    private static string? TryGetServiceStrategyName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services\zapret");
            return key?.GetValue("zapret-discord-youtube") as string;
        }
        catch
        {
            return null;
        }
    }

    public async Task StartStrategyAsync(string batFileName, CancellationToken ct = default, bool quickSwitch = false)
    {
        var batPath = Path.Combine(_paths.Root, batFileName);
        if (!File.Exists(batPath))
            throw new FileNotFoundException("Strategy file not found", batPath);

        if (IsRunning())
            await StopStrategyAsync(ct);

        if (quickSwitch)
            await Task.Delay(300, ct);

        await LaunchWinwsAsync(batFileName, ct);
    }

    public async Task StopStrategyAsync(CancellationToken ct = default)
    {
        foreach (var p in Process.GetProcessesByName("winws"))
        {
            try { p.Kill(true); } catch { /* ignore */ }
        }

        for (var i = 0; i < 20; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (Process.GetProcessesByName("winws").Length == 0)
                break;
            await Task.Delay(100, ct);
        }

        ZapretShutdownService.StopWinDivertServices();

        _lastStartedStrategy = null;
    }

    private async Task LaunchWinwsAsync(string batFileName, CancellationToken ct)
    {
        new ListFileService(_paths).EnsureUserLists();
        var args = StrategyBatParser.Parse(_paths, batFileName);
        if (string.IsNullOrWhiteSpace(args))
            throw new InvalidOperationException(Loc.T("strategy.winws_not_started"));

        var missing = ZapretHealth.MissingFiles(_paths)
            .Concat(ZapretHealth.MissingReferencedFiles(args))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missing.Count > 0)
            throw new FileNotFoundException(Loc.F("strategy.files_missing", string.Join(", ", missing)));

        var winwsPath = Path.Combine(_paths.Bin, "winws.exe");
        var psi = new ProcessStartInfo
        {
            FileName = winwsPath,
            Arguments = args,
            WorkingDirectory = _paths.Bin,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (WindowsAdmin.IsAdministrator())
        {
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
        }
        else
        {
            psi.UseShellExecute = true;
            psi.Verb = "runas";
        }

        try
        {
            if (Process.Start(psi) is null)
                throw new InvalidOperationException(Loc.T("strategy.winws_not_started"));
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException(Loc.T("strategy.uac_cancelled"));
        }

        _lastStartedStrategy = Path.GetFileNameWithoutExtension(batFileName);

        for (var i = 0; i < 10; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (IsRunning())
                return;
            await Task.Delay(100, ct);
        }

        if (!IsRunning())
            throw new InvalidOperationException(Loc.T("strategy.winws_not_started"));
    }

    public string ReadStrategyContent(string batFileName) =>
        File.ReadAllText(Path.Combine(_paths.Root, batFileName));

    public void SaveStrategyContent(string batFileName, string content)
    {
        var path = Path.Combine(_paths.Root, batFileName);
        File.WriteAllText(path, content);
    }

    public string CreateCustomStrategy(string baseStrategy, string newName)
    {
        if (!newName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            newName += ".bat";

        var basePath = Path.Combine(_paths.Root, baseStrategy);
        var newPath = Path.Combine(_paths.Root, newName);
        if (File.Exists(newPath))
            throw new InvalidOperationException("File already exists");

        File.Copy(basePath, newPath);
        return newName;
    }

    public bool CanDeleteStrategy(string batFileName)
    {
        if (batFileName.StartsWith("service", StringComparison.OrdinalIgnoreCase))
            return false;
        if (BundledStrategiesService.IsProtected(batFileName))
            return false;
        return true;
    }

    public void DeleteStrategy(string batFileName)
    {
        if (!CanDeleteStrategy(batFileName))
            throw new InvalidOperationException(Loc.T("strategies.cannot_delete_service"));

        var path = Path.Combine(_paths.Root, batFileName);
        if (!File.Exists(path))
            throw new FileNotFoundException(Loc.T("common.file_not_found"), batFileName);

        File.Delete(path);
    }

    public string RenameStrategy(string oldName, string newName)
    {
        if (!newName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            newName += ".bat";

        if (oldName.Equals(newName, StringComparison.OrdinalIgnoreCase))
            return oldName;

        if (oldName.StartsWith("service", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(Loc.T("strategies.cannot_rename_service"));

        var oldPath = Path.Combine(_paths.Root, oldName);
        var newPath = Path.Combine(_paths.Root, newName);
        if (!File.Exists(oldPath))
            throw new FileNotFoundException(Loc.T("common.file_not_found"), oldName);
        if (File.Exists(newPath))
            throw new InvalidOperationException(Loc.T("strategies.file_exists"));

        File.Move(oldPath, newPath);
        return newName;
    }

    public async Task<string> GetServiceStrategyAsync()
    {
        var script = $@"
            $val = (Get-ItemProperty -Path 'HKLM:\System\CurrentControlSet\Services\zapret' -Name 'zapret-discord-youtube' -ErrorAction SilentlyContinue).'zapret-discord-youtube'
            if ($val) {{ Write-Output $val }}
        ";
        var output = new List<string>();
        var runner = new ProcessRunner();
        runner.OutputReceived += output.Add;
        await runner.RunPowerShellAsync(script);
        return output.FirstOrDefault() ?? "";
    }
}
