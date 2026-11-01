using System.Text.RegularExpressions;

namespace Aeroway.Services;

public static class ZapretHealth
{
    public const string GuideUrl = "https://raccoonlaptop.github.io/Aeroway/";

    public static IReadOnlyList<string> MissingFiles(ZapretPaths paths)
    {
        var missing = new List<string>();
        if (!File.Exists(Path.Combine(paths.Bin, "winws.exe")))
            missing.Add("winws.exe");
        if (!File.Exists(Path.Combine(paths.Bin, "WinDivert.dll")))
            missing.Add("WinDivert.dll");
        if (!File.Exists(Path.Combine(paths.Bin, "WinDivert64.sys")))
            missing.Add("WinDivert64.sys");
        return missing;
    }

    public static IReadOnlyList<string> MissingReferencedFiles(string args)
    {
        var missing = new List<string>();
        foreach (Match match in Regex.Matches(args, @"""(?<path>[^""]+\.(?:bin|txt))""|(?<path>[^\s""]+\.(?:bin|txt))"))
        {
            var path = match.Groups["path"].Value;
            if (File.Exists(path))
                continue;
            var name = Path.GetFileName(path);
            if (!missing.Contains(name, StringComparer.OrdinalIgnoreCase))
                missing.Add(name);
        }

        return missing;
    }
}
