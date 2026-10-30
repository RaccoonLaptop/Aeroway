using System.Net;
using ZapretUI.Services;
using Xunit;

namespace ZapretUI.Tests;

public class HostsListTests
{
    [Fact]
    public void Sections_group_named_services_and_skip_blocks()
    {
        const string content = """
            # Последнее обновление: 29 августа 2026
            157.240.245.174 instagram.com
            0.0.0.0 ads.example
            # OpenAI
            193.233.112.68 chatgpt.com
            193.233.112.68 api.openai.com
            # Блокировка
            0.0.0.0 bad.example
            """;

        var sections = HostsListParser.ParseSections(content, "geohide");

        Assert.Equal(2, sections.Count);
        Assert.Equal("geohide-other", sections[0].Id);
        Assert.Equal(["157.240.245.174 instagram.com"], sections[0].Lines);
        Assert.Equal("OpenAI", sections[1].Title);
        Assert.Equal(2, sections[1].Lines.Count);
    }

    [Fact]
    public void SetBlock_replaces_only_its_own_lines()
    {
        var path = Path.Combine(Path.GetTempPath(), "aeroway-hosts-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(path, "127.0.0.1 localhost\r\n1.1.1.1 old.example\r\n");
            SystemHostsFile.SetBlock("flowseal", ["9.9.9.9 old.example", "9.9.9.9 new.example"], path);
            SystemHostsFile.SetBlock("geohide-openai", ["8.8.8.8 chatgpt.com"], path);

            var text = File.ReadAllText(path);
            Assert.Contains("127.0.0.1 localhost", text, StringComparison.Ordinal);
            Assert.Contains("# Aeroway:flowseal", text, StringComparison.Ordinal);
            Assert.Contains("9.9.9.9 old.example", text, StringComparison.Ordinal);
            Assert.DoesNotContain("1.1.1.1 old.example", text, StringComparison.Ordinal);
            Assert.Contains("8.8.8.8 chatgpt.com", text, StringComparison.Ordinal);

            Assert.True(SystemHostsFile.BlockMatches("flowseal", ["9.9.9.9 old.example", "9.9.9.9 new.example"], path));
            Assert.False(SystemHostsFile.BlockMatches("flowseal", ["1.1.1.1 other.example"], path));

            SystemHostsFile.RemoveBlock("flowseal", path);
            text = File.ReadAllText(path);
            Assert.DoesNotContain("new.example", text, StringComparison.Ordinal);
            Assert.Contains("chatgpt.com", text, StringComparison.Ordinal);
            Assert.Contains("127.0.0.1 localhost", text, StringComparison.Ordinal);

            SystemHostsFile.SetBlock("malw-openai", ["1.2.3.4 old.malw.example"], path);
            SystemHostsFile.SetBlock("geohide-adobe", ["9.9.9.9 adobe.example"], path);
            Assert.Equal(1, SystemHostsFile.RemoveBlocksByPrefix("malw-", path));
            text = File.ReadAllText(path);
            Assert.DoesNotContain("old.malw.example", text, StringComparison.Ordinal);
            Assert.Contains("adobe.example", text, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Bundled_geohide_list_parses_into_unique_sections()
    {
        var path = Path.Combine(RepoRoot(), "Assets", "hosts", "geohide.hosts");
        var sections = HostsListParser.ParseSections(File.ReadAllText(path), "geohide");

        Assert.NotEmpty(sections);
        Assert.All(sections, section =>
        {
            Assert.NotEmpty(section.Lines);
            Assert.InRange(section.Id.Length, 1, 48);
            Assert.DoesNotContain("malw", section.Id, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Equal(sections.Count, sections.Select(section => section.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(sections, section => section.Lines.Any(line => line.Contains("chatgpt.com", StringComparison.OrdinalIgnoreCase)));
        Assert.False(HostsAddressRefresher.HasDuplicateHosts(File.ReadAllText(path)));
        var grok = Assert.Single(sections, section => section.Title == "Grok AI");
        Assert.Contains("104.18.28.234 grok.com", grok.Lines);
        Assert.Contains("104.18.29.234 grok.com", grok.Lines);
        Assert.Contains("104.18.18.80 x.ai", grok.Lines);
        Assert.Contains("104.18.19.80 x.ai", grok.Lines);
    }

    [Fact]
    public async Task Refresh_writes_every_address_and_keeps_previous_when_dns_fails()
    {
        const string content = """
            # OpenAI
            1.1.1.1 chatgpt.com
            2.2.2.2 chatgpt.com
            3.3.3.3 api.openai.com
            # Grok AI
            9.9.9.9 chatgpt.com
            8.8.8.8 grok.com
            """;

        var result = await HostsAddressRefresher.RefreshAsync(content, (host, _) =>
            Task.FromResult<IReadOnlyList<IPAddress>>(host == "grok.com"
                ? [IPAddress.Parse("104.18.29.234"), IPAddress.Parse("104.18.28.234")]
                : []));

        Assert.Contains("1.1.1.1 chatgpt.com", result.Content, StringComparison.Ordinal);
        Assert.Contains("2.2.2.2 chatgpt.com", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("9.9.9.9", result.Content, StringComparison.Ordinal);
        Assert.Contains("104.18.28.234 grok.com", result.Content, StringComparison.Ordinal);
        Assert.Contains("104.18.29.234 grok.com", result.Content, StringComparison.Ordinal);
        Assert.Contains("3.3.3.3 api.openai.com", result.Content, StringComparison.Ordinal);
        Assert.Equal(1, result.Resolved);
        Assert.Equal(2, result.Kept);
        Assert.False(HostsAddressRefresher.HasDuplicateHosts(result.Content));
    }

    [Fact]
    public void Extra_host_from_the_current_list_survives_a_fresh_download()
    {
        const string refreshed = """
            # Grok AI
            104.18.28.234 grok.com
            """;
        const string previous = """
            # Grok AI
            104.18.28.234 grok.com
            172.66.0.227 wr.x.com
            """;

        var merged = HostsAddressRefresher.KeepExtraHosts(refreshed, previous);
        var grok = Assert.Single(HostsListParser.ParseSections(merged, "geohide"), section => section.Title == "Grok AI");
        Assert.Equal(["104.18.28.234 grok.com", "172.66.0.227 wr.x.com"], grok.Lines);
    }

    [Fact]
    public async Task RefreshLines_updates_only_that_lists_addresses()
    {
        var refresh = await HostsAddressRefresher.RefreshLinesAsync(
            ["1.1.1.1 grok.com", "2.2.2.2 grok.com", "9.9.9.9 x.ai"],
            (host, _) => Task.FromResult<IReadOnlyList<IPAddress>>(host == "grok.com"
                ? [IPAddress.Parse("104.18.29.234"), IPAddress.Parse("104.18.28.234")]
                : []));

        Assert.Equal(["104.18.28.234 grok.com", "104.18.29.234 grok.com", "9.9.9.9 x.ai"], refresh.Lines);
        Assert.Equal(1, refresh.Resolved);
        Assert.Equal(1, refresh.Kept);

        const string content = """
            # OpenAI
            1.1.1.1 chatgpt.com

            # Grok AI
            8.8.8.8 grok.com
            8.8.4.4 grok.com
            """;
        var updated = HostsAddressRefresher.ReplaceNamedSection(content, "Grok AI", refresh.Lines.Take(2).ToArray());
        Assert.Contains("1.1.1.1 chatgpt.com", updated, StringComparison.Ordinal);
        Assert.Contains("104.18.28.234 grok.com", updated, StringComparison.Ordinal);
        Assert.Contains("104.18.29.234 grok.com", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("8.8.8.8", updated, StringComparison.Ordinal);
        var grok = Assert.Single(HostsListParser.ParseSections(updated, "geohide"), section => section.Title == "Grok AI");
        Assert.Equal(["104.18.28.234 grok.com", "104.18.29.234 grok.com"], grok.Lines);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ZapretUI.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
