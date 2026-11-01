using System.Diagnostics;
using Microsoft.Win32;

namespace Aeroway.Services;

/// <summary>
/// Переносит уже установленную копию из %LOCALAPPDATA%\ZapretUI в Aeroway.
/// Текущий установщик обновления всё ещё запускает ZapretUI.exe из старой папки,
/// поэтому перенос делается при первом старте новой версии.
/// </summary>
public static class InstallDirMigration
{
    public const string FolderName = "Aeroway";
    public const string LegacyFolderName = "ZapretUI";
    internal const string UninstallKey =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F4E2A91-3C7D-4B6E-9F12-0A1B2C3D4E5F}_is1";

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        FolderName);

    public static string LegacyDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        LegacyFolderName);

    public static bool TryRelaunchIntoAerowayFolder() =>
        TryRelaunchIntoAerowayFolder(MigrationEnvironment.CreateLive());

    internal static bool TryRelaunchIntoAerowayFolder(MigrationEnvironment env)
    {
        try
        {
            var current = env.BaseDirectory.TrimEnd('\\', '/');
            var leaf = Path.GetFileName(current);
            if (!leaf.Equals(LegacyFolderName, StringComparison.OrdinalIgnoreCase))
                return false;

            var dest = env.DataDirectory;
            if (string.Equals(Path.GetFullPath(current), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                return false;

            CopyTree(current, dest);
            var exe = FirstExistingExe(dest);
            if (exe is null)
                return false;

            var key = env.Effects.OpenUninstallKey();
            if (key is not null)
            {
                using (key)
                    RewriteUninstallRegistry(key, current, dest, exe);
            }

            RewriteShortcuts(env, exe, dest);
            RemoveLegacyShortcuts(env);

            env.Effects.StartProcess(exe, dest, env.CommandLineArgs);
            ScheduleOldFolderDelete(env, current);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void RemoveLeftoverLegacyFolder() =>
        RemoveLeftoverLegacyFolder(
            MigrationEnvironment.CreateLive(),
            ZapretPaths.IsValidZapretRoot(ZapretPaths.GetBundledZapretPath()));

    internal static void RemoveLeftoverLegacyFolder(MigrationEnvironment env, bool bundledZapretValid)
    {
        try
        {
            var current = env.BaseDirectory.TrimEnd('\\', '/');
            if (Path.GetFileName(current).Equals(LegacyFolderName, StringComparison.OrdinalIgnoreCase))
                return;
            if (!Directory.Exists(env.LegacyDataDirectory))
                return;
            if (!bundledZapretValid)
                return;

            ScheduleOldFolderDelete(env, env.LegacyDataDirectory);
        }
        catch
        {
            /* leave the old folder if it is still in use */
        }
    }

    public static void RemoveLegacyExeCopy() =>
        RemoveLegacyExeCopy(MigrationEnvironment.CreateLive());

    internal static void RemoveLegacyExeCopy(MigrationEnvironment env)
    {
        try
        {
            var current = env.ProcessPath;
            if (string.IsNullOrEmpty(current))
                return;
            if (Path.GetFileName(current).Equals("ZapretUI.exe", StringComparison.OrdinalIgnoreCase))
                return;

            var legacy = Path.Combine(env.BaseDirectory, "ZapretUI.exe");
            if (File.Exists(legacy))
                File.Delete(legacy);
        }
        catch
        {
            /* file still in use */
        }
    }

    public static string? FirstExistingExe(string directory)
    {
        foreach (var name in new[] { "Aeroway.exe", "ZapretUI.exe" })
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private static void CopyTree(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            var target = Path.Combine(dest, Path.GetFileName(file));
            try
            {
                File.Copy(file, target, overwrite: true);
            }
            catch (IOException)
            {
                /* winws or the running exe can keep a file open */
            }
        }

        foreach (var dir in Directory.GetDirectories(source))
            CopyTree(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    private static void RewriteUninstallRegistry(IUninstallRegistryKey key, string oldDir, string newDir, string exe)
    {
        foreach (var name in key.ValueNames.ToArray())
        {
            if (key.GetValue(name) is not string text)
                continue;
            if (text.Contains(oldDir, StringComparison.OrdinalIgnoreCase))
                key.SetValue(name, text.Replace(oldDir, newDir, StringComparison.OrdinalIgnoreCase));
        }

        key.SetValue("DisplayName", "Aeroway");
        key.SetValue("DisplayIcon", exe);
        key.SetValue("InstallLocation", newDir + "\\");
    }

    private static void RewriteShortcuts(MigrationEnvironment env, string exe, string workDir)
    {
        WriteShortcut(Path.Combine(env.ProgramsDirectory, "Aeroway.lnk"), exe, workDir);
        var desktopLink = Path.Combine(env.DesktopDirectory, "Aeroway.lnk");
        if (File.Exists(desktopLink))
            WriteShortcut(desktopLink, exe, workDir);
    }

    private static void WriteShortcut(string linkPath, string exe, string workDir)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
            return;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(linkPath);
        shortcut.TargetPath = exe;
        shortcut.WorkingDirectory = workDir;
        shortcut.IconLocation = exe + ",0";
        shortcut.Description = "Aeroway";
        shortcut.Save();
    }

    private static void RemoveLegacyShortcuts(MigrationEnvironment env)
    {
        TryDeleteFile(Path.Combine(env.ProgramsDirectory, "Zapret UI.lnk"));
        TryDeleteFile(Path.Combine(env.DesktopDirectory, "Zapret UI.lnk"));
        TryDeleteDirectory(Path.Combine(env.ProgramsDirectory, "Zapret UI"));
    }

    private static void ScheduleOldFolderDelete(MigrationEnvironment env, string oldDir)
    {
        var full = Path.GetFullPath(oldDir).TrimEnd('\\', '/');
        if (!Path.GetFileName(full).Equals(LegacyFolderName, StringComparison.OrdinalIgnoreCase))
            return;

        var currentExe = env.ProcessPath ?? "";
        var waitPid = currentExe.StartsWith(full + "\\", StringComparison.OrdinalIgnoreCase)
                      || currentExe.Equals(Path.Combine(full, "ZapretUI.exe"), StringComparison.OrdinalIgnoreCase)
                      || currentExe.Equals(Path.Combine(full, "Aeroway.exe"), StringComparison.OrdinalIgnoreCase)
            ? env.ProcessId
            : 0;

        var script = Path.Combine(env.BaseDirectory, "Scripts", "remove-legacy-zapretui.ps1");
        if (!File.Exists(script))
            return;

        env.Effects.StartHiddenPowerShell(
            $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\" -LegacyPath \"{full}\" -WaitPid {waitPid}");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            /* shortcut in use */
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            /* folder in use */
        }
    }
}

internal sealed record MigrationEnvironment
{
    public required string BaseDirectory { get; init; }
    public string? ProcessPath { get; init; }
    public int ProcessId { get; init; }
    public required string LocalAppData { get; init; }
    public required string ProgramsDirectory { get; init; }
    public required string DesktopDirectory { get; init; }
    public IReadOnlyList<string> CommandLineArgs { get; init; } = [];
    public required IMigrationEffects Effects { get; init; }

    public string DataDirectory => Path.Combine(LocalAppData, InstallDirMigration.FolderName);

    public string LegacyDataDirectory => Path.Combine(LocalAppData, InstallDirMigration.LegacyFolderName);

    public static MigrationEnvironment CreateLive() => new()
    {
        BaseDirectory = AppContext.BaseDirectory,
        ProcessPath = Environment.ProcessPath,
        ProcessId = Environment.ProcessId,
        LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProgramsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        DesktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        CommandLineArgs = Environment.GetCommandLineArgs().Skip(1).ToArray(),
        Effects = LiveMigrationEffects.Instance
    };
}

internal interface IMigrationEffects
{
    IUninstallRegistryKey? OpenUninstallKey();
    void StartProcess(string exe, string workingDirectory, IReadOnlyList<string> arguments);
    void StartHiddenPowerShell(string arguments);
}

internal interface IUninstallRegistryKey : IDisposable
{
    IEnumerable<string> ValueNames { get; }
    object? GetValue(string name);
    void SetValue(string name, object value);
}

file sealed class LiveMigrationEffects : IMigrationEffects
{
    public static readonly LiveMigrationEffects Instance = new();

    public IUninstallRegistryKey? OpenUninstallKey()
    {
        var key = Registry.CurrentUser.OpenSubKey(InstallDirMigration.UninstallKey, writable: true);
        return key is null ? null : new RegistryUninstallKey(key);
    }

    public void StartProcess(string exe, string workingDirectory, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true
        };
        foreach (var arg in arguments)
            start.ArgumentList.Add(arg);
        Process.Start(start);
    }

    public void StartHiddenPowerShell(string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private sealed class RegistryUninstallKey : IUninstallRegistryKey
    {
        private readonly RegistryKey _key;

        public RegistryUninstallKey(RegistryKey key) => _key = key;

        public IEnumerable<string> ValueNames => _key.GetValueNames();

        public object? GetValue(string name) => _key.GetValue(name);

        public void SetValue(string name, object value) => _key.SetValue(name, value);

        public void Dispose() => _key.Dispose();
    }
}
