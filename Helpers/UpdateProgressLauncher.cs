using System.Diagnostics;
using System.IO;
using ZapretUI.Services;

namespace ZapretUI.Helpers;

public static class UpdateProgressLauncher
{
    public const string ArgFlag = "--update-progress";

    public static void Start(string logFile, string targetVersion)
    {
        try
        {
            if (File.Exists(logFile))
                File.Delete(logFile);
        }
        catch { /* ignore */ }

        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            exe = InstallDirMigration.FirstExistingExe(AppContext.BaseDirectory);
        if (string.IsNullOrEmpty(exe))
            return;

        var version = string.IsNullOrWhiteSpace(targetVersion) ? "?" : targetVersion.Trim();
        var args =
            $"{ArgFlag} --log \"{logFile}\" --version \"{version}\"";

        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory
        });
    }

    public static bool TryParseArgs(string[] args, out string logFile, out string version)
    {
        logFile = "";
        version = "";
        if (args is null || args.Length == 0)
            return false;

        var hasFlag = false;
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.Equals(ArgFlag, StringComparison.OrdinalIgnoreCase))
            {
                hasFlag = true;
                continue;
            }

            if (TryReadValue(args, ref i, "--log", out var log))
                logFile = log;
            else if (TryReadValue(args, ref i, "--version", out var ver))
                version = ver;
        }

        return hasFlag && !string.IsNullOrWhiteSpace(logFile);
    }

    private static bool TryReadValue(string[] args, ref int i, string name, out string value)
    {
        value = "";
        var a = args[i];
        if (a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = a[(name.Length + 1)..].Trim('"');
            return true;
        }

        if (!a.Equals(name, StringComparison.OrdinalIgnoreCase))
            return false;

        if (i + 1 < args.Length)
        {
            value = args[++i].Trim('"');
            return true;
        }

        return false;
    }
}
