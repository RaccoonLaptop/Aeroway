using System.IO;
using System.Text.RegularExpressions;
using Aeroway.Models;

namespace Aeroway.Services;

public static class TestTargetsLoader
{
    private static readonly Regex TargetLine = new(
        @"^\s*(\w+)\s*=\s*""(.+)""\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<TestTargetDefinition> LoadDefinitions(ZapretPaths paths)
    {
        var file = Path.Combine(paths.Utils, "targets.txt");
        if (!File.Exists(file))
            return DefaultDefinitions();

        var list = new List<TestTargetDefinition>();
        foreach (var line in File.ReadAllLines(file))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            var m = TargetLine.Match(line);
            if (!m.Success) continue;

            var value = m.Groups[2].Value.Trim();
            list.Add(new TestTargetDefinition
            {
                Name = m.Groups[1].Value.Trim(),
                PingOnly = value.StartsWith("PING:", StringComparison.OrdinalIgnoreCase)
            });
        }

        return list.Count > 0 ? list : DefaultDefinitions();
    }

    private static IReadOnlyList<TestTargetDefinition> DefaultDefinitions() =>
    [
        new() { Name = "DiscordMain", PingOnly = false },
        new() { Name = "DiscordGateway", PingOnly = false },
        new() { Name = "DiscordCDN", PingOnly = false },
        new() { Name = "DiscordUpdates", PingOnly = false },
        new() { Name = "YouTubeWeb", PingOnly = false },
        new() { Name = "YouTubeShort", PingOnly = false },
        new() { Name = "YouTubeImage", PingOnly = false },
        new() { Name = "YouTubeVideoRedirect", PingOnly = false },
        new() { Name = "GoogleMain", PingOnly = false },
        new() { Name = "GoogleGstatic", PingOnly = false },
        new() { Name = "CloudflareWeb", PingOnly = false },
        new() { Name = "CloudflareCDN", PingOnly = false },
        new() { Name = "CloudflareDNS1111", PingOnly = true },
        new() { Name = "CloudflareDNS1001", PingOnly = true },
        new() { Name = "GoogleDNS8888", PingOnly = true },
        new() { Name = "GoogleDNS8844", PingOnly = true },
        new() { Name = "Quad9DNS9999", PingOnly = true }
    ];
}
