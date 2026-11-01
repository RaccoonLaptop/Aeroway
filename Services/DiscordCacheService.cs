using System.Diagnostics;
using System.IO;
using Aeroway.Helpers;

namespace Aeroway.Services;

public static class DiscordCacheService
{
    private static readonly (string Process, string Name, string CacheDir)[] Targets =
    [
        ("Discord.exe", "Discord", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discord")),
        ("DiscordPTB.exe", "Discord PTB", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discordptb")),
        ("DiscordCanary.exe", "Discord Canary", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discordcanary")),
        ("DiscordDevelopment.exe", "Discord Development", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discorddevelopment"))
    ];

    public static IReadOnlyList<string> ClearCaches()
    {
        var lines = new List<string>();
        var found = false;

        foreach (var (process, name, cacheDir) in Targets)
        {
            if (!Directory.Exists(cacheDir))
                continue;

            found = true;
            lines.AddRange(ClearOne(process, name, cacheDir));
        }

        if (!found)
            lines.Add(Loc.T("diag.discord_cache_not_found"));

        return lines;
    }

    private static IEnumerable<string> ClearOne(string processName, string displayName, string cacheDir)
    {
        var lines = new List<string>();

        if (IsProcessRunning(processName))
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName)))
                {
                    proc.Kill();
                    proc.WaitForExit(5000);
                }
                lines.Add(Loc.F("diag.discord_cache_closed", displayName));
            }
            catch
            {
                lines.Add(Loc.F("diag.discord_cache_close_fail", displayName));
            }
        }

        foreach (var subDir in new[] { "Cache", "Code Cache", "GPUCache" })
        {
            var path = Path.Combine(cacheDir, subDir);
            if (!Directory.Exists(path))
            {
                lines.Add(Loc.F("diag.discord_cache_missing_dir", path));
                continue;
            }

            try
            {
                Directory.Delete(path, true);
                lines.Add(Loc.F("diag.discord_cache_deleted", path));
            }
            catch
            {
                lines.Add(Loc.F("diag.discord_cache_delete_fail", path));
            }
        }

        return lines;
    }

    private static bool IsProcessRunning(string processName) =>
        Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName)).Length > 0;
}
