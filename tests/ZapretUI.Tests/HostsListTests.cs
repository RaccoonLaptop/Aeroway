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
