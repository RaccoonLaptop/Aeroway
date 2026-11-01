using System.IO;
using System.Text.RegularExpressions;
using Aeroway.Models;

namespace Aeroway.Services;

public static class TestTargetsLoader
{
    private static readonly Regex TargetLine = new(
        @"^\s*(\w+)\s*=\s*""(.+)""\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<TestTargetDefinition> LoadDefinitions(ZapretPaths paths) =>
        LoadEndpoints(paths)
            .Select(endpoint => new TestTargetDefinition
            {
                Name = endpoint.Name,
                PingOnly = endpoint.PingOnly
            })
            .ToList();

    public static IReadOnlyList<TestTargetEndpoint> LoadEndpoints(ZapretPaths paths)
    {
        var file = Path.Combine(paths.Utils, "targets.txt");
        if (!File.Exists(file))
            return DefaultEndpoints();

        var list = new List<TestTargetEndpoint>();
        foreach (var line in File.ReadAllLines(file))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            var match = TargetLine.Match(line);
            if (!match.Success)
                continue;

            list.Add(ParseEndpoint(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim()));
        }

        return list.Count > 0 ? list : DefaultEndpoints();
    }

    internal static TestTargetEndpoint ParseEndpoint(string name, string value)
    {
        if (value.StartsWith("PING:", StringComparison.OrdinalIgnoreCase))
        {
            return new TestTargetEndpoint
            {
                Name = name,
                PingHost = value["PING:".Length..].Trim()
            };
        }

        return new TestTargetEndpoint
        {
            Name = name,
            Url = value,
            PingHost = HostOf(value)
        };
    }

    private static string HostOf(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            return uri.Host;

        var host = value;
        var scheme = host.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
            host = host[(scheme + 3)..];
        var slash = host.IndexOf('/');
        if (slash >= 0)
            host = host[..slash];
        return host;
    }

    private static IReadOnlyList<TestTargetEndpoint> DefaultEndpoints() =>
    [
        ParseEndpoint("DiscordMain", "https://discord.com"),
        ParseEndpoint("DiscordGateway", "https://gateway.discord.gg"),
        ParseEndpoint("DiscordCDN", "https://cdn.discordapp.com"),
        ParseEndpoint("DiscordUpdates", "https://updates.discord.com"),
        ParseEndpoint("YouTubeWeb", "https://www.youtube.com"),
        ParseEndpoint("YouTubeShort", "https://youtu.be"),
        ParseEndpoint("YouTubeImage", "https://i.ytimg.com"),
        ParseEndpoint("YouTubeVideoRedirect", "https://redirector.googlevideo.com"),
        ParseEndpoint("GoogleMain", "https://www.google.com"),
        ParseEndpoint("GoogleGstatic", "https://www.gstatic.com"),
        ParseEndpoint("CloudflareWeb", "https://www.cloudflare.com"),
        ParseEndpoint("CloudflareCDN", "https://cdnjs.cloudflare.com"),
        ParseEndpoint("CloudflareDNS1111", "PING:1.1.1.1"),
        ParseEndpoint("CloudflareDNS1001", "PING:1.0.0.1"),
        ParseEndpoint("GoogleDNS8888", "PING:8.8.8.8"),
        ParseEndpoint("GoogleDNS8844", "PING:8.8.4.4"),
        ParseEndpoint("Quad9DNS9999", "PING:9.9.9.9")
    ];
}
