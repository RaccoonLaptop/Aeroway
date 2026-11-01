using System.IO.Compression;

namespace Aeroway.Services;

public static class UserDataPack
{
    public static void Export(string zapretRoot, string zipPath)
    {
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        if (Directory.Exists(zapretRoot))
        {
            foreach (var bat in Directory.GetFiles(zapretRoot, "*.bat"))
            {
                var name = Path.GetFileName(bat);
                if (IsServiceBat(name))
                    continue;
                zip.CreateEntryFromFile(bat, "strategies/" + name);
            }
        }

        var lists = Path.Combine(zapretRoot, "lists");
        if (Directory.Exists(lists))
        {
            foreach (var file in Directory.GetFiles(lists, "*-user.txt"))
                zip.CreateEntryFromFile(file, "lists/" + Path.GetFileName(file));
        }

        var gameFilter = Path.Combine(zapretRoot, "utils", "game_filter.enabled");
        if (File.Exists(gameFilter))
            zip.CreateEntryFromFile(gameFilter, "utils/game_filter.enabled");
    }

    public static int Import(string zapretRoot, string zipPath)
    {
        var count = 0;
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (!TryResolveDest(zapretRoot, entry, out var dest))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            entry.ExtractToFile(dest, overwrite: true);
            count++;
        }

        return count;
    }

    private static bool TryResolveDest(string zapretRoot, ZipArchiveEntry entry, out string dest)
    {
        dest = "";
        var name = entry.Name;
        if (string.IsNullOrEmpty(name) || name.Contains("..", StringComparison.Ordinal))
            return false;

        var full = entry.FullName.Replace('\\', '/');
        if (full.StartsWith("strategies/", StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) &&
            !IsServiceBat(name))
        {
            dest = Path.Combine(zapretRoot, name);
            return true;
        }

        if (full.StartsWith("lists/", StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith("-user.txt", StringComparison.OrdinalIgnoreCase))
        {
            dest = Path.Combine(zapretRoot, "lists", name);
            return true;
        }

        if (full.Equals("utils/game_filter.enabled", StringComparison.OrdinalIgnoreCase))
        {
            dest = Path.Combine(zapretRoot, "utils", "game_filter.enabled");
            return true;
        }

        return false;
    }

    private static bool IsServiceBat(string name) =>
        name.StartsWith("service", StringComparison.OrdinalIgnoreCase);
}
