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
        if (!File.Exists(Path.Combine(paths.Bin, WinDivertHideService.DllFileName)))
            missing.Add(WinDivertHideService.DllFileName);
        var stockDriver = File.Exists(Path.Combine(paths.Bin, WinDivertHideService.StockDriverFileName));
        var hideDriver = File.Exists(Path.Combine(paths.Bin, WinDivertHideService.HideDriverFileName));
        if (!stockDriver && !hideDriver)
            missing.Add(WinDivertHideService.StockDriverFileName);
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
