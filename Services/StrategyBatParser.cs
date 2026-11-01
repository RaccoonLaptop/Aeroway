using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Aeroway.Services;

public static class StrategyBatParser
{
    private static readonly string[] ArgsWithValue = ["sni", "host", "altorder"];

    public static string Parse(ZapretPaths paths, string batFileName)
    {
        var batPath = Path.Combine(paths.Root, batFileName);
        if (!File.Exists(batPath))
            return "";

        var gf = ServiceSettingsService.ReadGameFilter(paths.Utils);
        var binPath = paths.Bin.TrimEnd('\\') + "\\";
        var listsPath = paths.Lists.TrimEnd('\\') + "\\";
        var capture = false;
        var mergeArgs = 0;
        var result = new StringBuilder();

        foreach (var rawLine in File.ReadLines(batPath))
        {
            var line = rawLine.Replace('!', '\u0001');

            if (line.Contains("%BIN%winws.exe", StringComparison.OrdinalIgnoreCase))
            {
                capture = true;
                line = Regex.Replace(line, @".*[%""']?%BIN%winws\.exe[%""']?\s*", "", RegexOptions.IgnoreCase);
            }
            else if (line.Contains("winws.exe", StringComparison.OrdinalIgnoreCase))
            {
                capture = true;
                line = Regex.Replace(line, @".*winws\.exe[%""']?\s*", "", RegexOptions.IgnoreCase);
            }
            else if (!capture)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            foreach (var token in line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token == "^")
                    continue;

                var arg = token;
                if (arg.StartsWith("--", StringComparison.Ordinal) && mergeArgs != 0)
                    mergeArgs = 0;

                var a = arg
                    .Replace("%GameFilterTCP%", gf.ResolvedTcp)
                    .Replace("%GameFilterUDP%", gf.ResolvedUdp)
                    .Replace("%GameFilter%", gf.ResolvedFilter)
                    .Replace("%BIN%", binPath)
                    .Replace("%LISTS%", listsPath);

                if (a.Length >= 2 && a.StartsWith('"') && a.EndsWith('"'))
                {
                    var inner = a[1..^1];
                    a = inner.Contains(':') ? $"\"{inner}\""
                        : inner.StartsWith('@') ? $"\"{Path.Combine(paths.Root, inner[1..])}\""
                        : $"\"{inner}\"";
                }

                if (mergeArgs == 1)
                    result.Append(',').Append(a);
                else if (mergeArgs == 3)
                {
                    result.Append('=').Append(a);
                    mergeArgs = 1;
                }
                else
                    result.Append(' ').Append(a);

                if (a.StartsWith("--", StringComparison.Ordinal))
                    mergeArgs = 2;
                else if (mergeArgs >= 1)
                {
                    if (mergeArgs == 2)
                        mergeArgs = 1;

                    foreach (var v in ArgsWithValue)
                    {
                        if (string.Equals(arg, v, StringComparison.OrdinalIgnoreCase))
                        {
                            mergeArgs = 3;
                            break;
                        }
                    }
                }
            }
        }

        return result.ToString().Replace('\u0001', '!').Trim();
    }
}
