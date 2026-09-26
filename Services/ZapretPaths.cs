using System.IO;
using System.Text.RegularExpressions;

namespace ZapretUI.Services;

public sealed class ZapretPaths
{
    public string Root { get; }
    public string Bin => Path.Combine(Root, "bin");
    public string Lists => Path.Combine(Root, "lists");
    public string Utils => Path.Combine(Root, "utils");
    public string ServiceBat => Path.Combine(Root, "service.bat");

    public bool IsValid => IsValidZapretRoot(Root);

    public ZapretPaths(string? rootOverride = null)
    {
        Root = rootOverride ?? DetectRoot();
    }

    public static bool IsValidZapretRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var full = Path.GetFullPath(path);
            return File.Exists(Path.Combine(full, "service.bat")) &&
                   Directory.Exists(Path.Combine(full, "bin"));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Находит корень пакета Flowseal после распаковки (часто вложен в подпапку релиза).</summary>
    public static string? ResolvePackageRoot(string extractDir)
    {
        if (IsValidZapretRoot(extractDir))
            return Path.GetFullPath(extractDir);

        if (!Directory.Exists(extractDir))
            return null;

        foreach (var sub in Directory.GetDirectories(extractDir))
        {
            if (IsValidZapretRoot(sub))
                return Path.GetFullPath(sub);
        }

        return null;
    }

    public static string GetBundledZapretPath()
    {
        var baseDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        return Path.Combine(baseDir, "zapret");
    }

    public static string DetectRoot(string? savedPath = null)
    {
        var bundled = GetBundledZapretPath();
        if (IsValidZapretRoot(bundled))
            return Path.GetFullPath(bundled);

        if (IsValidZapretRoot(savedPath) && !IsLegacyInstallPath(savedPath))
            return Path.GetFullPath(savedPath!);

        var dir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        for (var i = 0; i < 8; i++)
        {
            if (IsValidZapretRoot(dir))
                return Path.GetFullPath(dir);

            var parent = Directory.GetParent(dir)?.FullName;
            if (parent is null) break;
            dir = parent;
        }

        // Рядом с папкой программы (ZapretUI-Program лежит внутри zapret или рядом)
        var sibling = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        if (IsValidZapretRoot(sibling))
            return sibling;

        return savedPath ?? sibling;
    }

    private static bool IsLegacyInstallPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        try
        {
            var full = Path.GetFullPath(path).TrimEnd('\\', '/');
            var legacy = InstallDirMigration.LegacyDataDirectory.TrimEnd('\\', '/');
            return full.Equals(legacy, StringComparison.OrdinalIgnoreCase)
                   || full.StartsWith(legacy + "\\", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public string GetLocalVersion()
    {
        var serviceVersionFile = Path.Combine(Root, ".service", "version.txt");
        if (File.Exists(serviceVersionFile))
        {
            var fromFile = NormalizeVersionText(File.ReadAllText(serviceVersionFile));
            if (!string.IsNullOrEmpty(fromFile))
                return fromFile;
        }

        if (!File.Exists(ServiceBat)) return "unknown";
        var text = File.ReadAllText(ServiceBat);
        var match = Regex.Match(text, @"set\s+""LOCAL_VERSION=([^""]+)""");
        return match.Success ? NormalizeVersionText(match.Groups[1].Value) : "unknown";
    }

    internal static string NormalizeVersionText(string? value) =>
        value?.Trim().Trim('\uFEFF', '\u200B') ?? "";

    public IEnumerable<string> GetStrategyFiles()
    {
        if (!IsValid) return [];
        return Directory.GetFiles(Root, "*.bat")
            .Select(Path.GetFileName)
            .Where(f => f is not null && !f.StartsWith("service", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => NaturalBatSortKey(f!), StringComparer.OrdinalIgnoreCase)
            .Cast<string>();
    }

    /// <summary>Как в utils/test zapret.ps1: числа в имени дополняются нулями для естественной сортировки.</summary>
    internal static string NaturalBatSortKey(string fileName) =>
        Regex.Replace(fileName, @"(\d+)", m => m.Value.PadLeft(8, '0'));

    public IEnumerable<string> GetListFiles()
    {
        if (!IsValid || !Directory.Exists(Lists)) return [];

        var all = Directory.GetFiles(Lists, "*.txt")
            .Select(Path.GetFileName)
            .Where(f => f is not null &&
                        !f.EndsWith(".backup", StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .ToList();

        string[] official =
        [
            "list-general.txt",
            "list-google.txt",
            "list-exclude.txt",
            "ipset-all.txt",
            "ipset-exclude.txt"
        ];

        var result = new List<string>();
        foreach (var name in official)
        {
            var match = all.FirstOrDefault(f => f.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                result.Add(match);
        }

        foreach (var user in all.Where(f => f.EndsWith("-user.txt", StringComparison.OrdinalIgnoreCase))
                                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            if (!result.Contains(user, StringComparer.OrdinalIgnoreCase))
                result.Add(user);
        }

        return result;
    }
}
