using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ZapretUI.Services;

/// <summary>
/// Переносит уже установленную копию из %LOCALAPPDATA%\ZapretUI в Aeroway.
/// Текущий установщик обновления всё ещё запускает ZapretUI.exe из старой папки,
/// поэтому перенос делается при первом старте новой версии.
/// </summary>
public static class InstallDirMigration
{
    public const string FolderName = "Aeroway";
    public const string LegacyFolderName = "ZapretUI";
    private const string UninstallKey =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F4E2A91-3C7D-4B6E-9F12-0A1B2C3D4E5F}_is1";

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        FolderName);

    public static string LegacyDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        LegacyFolderName);

    public static bool TryRelaunchIntoAerowayFolder()
    {
        try
        {
            var current = AppContext.BaseDirectory.TrimEnd('\\', '/');
            var leaf = Path.GetFileName(current);
            if (!leaf.Equals(LegacyFolderName, StringComparison.OrdinalIgnoreCase))
                return false;

            var dest = DataDirectory;
            if (string.Equals(Path.GetFullPath(current), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                return false;

            CopyTree(current, dest);
            var exe = FirstExistingExe(dest);
            if (exe is null)
                return false;

            RewriteUninstallRegistry(current, dest, exe);
            RewriteShortcuts(exe, dest);
            RemoveLegacyShortcuts();

            var start = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = dest,
                UseShellExecute = true
            };
            foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
                start.ArgumentList.Add(arg);
            Process.Start(start);
            ScheduleOldFolderDelete(current);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void RemoveLeftoverLegacyFolder()
    {
        try
        {
            var current = AppContext.BaseDirectory.TrimEnd('\\', '/');
            if (Path.GetFileName(current).Equals(LegacyFolderName, StringComparison.OrdinalIgnoreCase))
                return;
            if (!Directory.Exists(LegacyDataDirectory))
                return;
            if (!ZapretPaths.IsValidZapretRoot(ZapretPaths.GetBundledZapretPath()))
                return;

            ScheduleOldFolderDelete(LegacyDataDirectory);
        }
        catch
        {
            /* leave the old folder if it is still in use */
        }
    }

    public static void RemoveLegacyExeCopy()
    {
        try
        {
            var current = Environment.ProcessPath;
            if (string.IsNullOrEmpty(current))
                return;
            if (Path.GetFileName(current).Equals("ZapretUI.exe", StringComparison.OrdinalIgnoreCase))
                return;

            var legacy = Path.Combine(AppContext.BaseDirectory, "ZapretUI.exe");
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

    private static void RewriteUninstallRegistry(string oldDir, string newDir, string exe)
    {
        using var key = Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true);
        if (key is null)
            return;

        foreach (var name in key.GetValueNames())
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

    private static void RewriteShortcuts(string exe, string workDir)
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        WriteShortcut(Path.Combine(programs, "Aeroway.lnk"), exe, workDir);
        var desktopLink = Path.Combine(desktop, "Aeroway.lnk");
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

    private static void RemoveLegacyShortcuts()
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        TryDeleteFile(Path.Combine(programs, "Zapret UI.lnk"));
        TryDeleteFile(Path.Combine(desktop, "Zapret UI.lnk"));
        TryDeleteDirectory(Path.Combine(programs, "Zapret UI"));
    }

    private static void ScheduleOldFolderDelete(string oldDir)
    {
        if (!Path.GetFileName(oldDir).Equals(LegacyFolderName, StringComparison.OrdinalIgnoreCase))
            return;

        var script = Path.Combine(Path.GetTempPath(), "aeroway-move-" + Guid.NewGuid().ToString("N") + ".ps1");
        var pid = Environment.ProcessId;
        var literal = oldDir.Replace("'", "''");
        File.WriteAllText(script,
            "$deadline = (Get-Date).AddSeconds(30)\r\n" +
            $"while ((Get-Process -Id {pid} -ErrorAction SilentlyContinue) -and (Get-Date) -lt $$deadline) {{ Start-Sleep -Milliseconds 200 }}\r\n" +
            "Start-Sleep -Seconds 1\r\n" +
            $"Remove-Item -LiteralPath '{literal}' -Recurse -Force -ErrorAction SilentlyContinue\r\n" +
            "Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue\r\n");
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
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
