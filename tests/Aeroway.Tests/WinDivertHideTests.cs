using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aeroway.Services;
using Xunit;

namespace Aeroway.Tests;

public class WinDivertHideTests
{
    private const string StockDllSha256 = "c1e060ee19444a259b2162f8af0f3fe8c4428a1c6f694dce20de194ac8d7d9a2";
    private const string StockSysSha256 = "8da085332782708d8767bcace5327a6ec7283c17cfb85e40b03cd2323a90ddc2";
    private const string HideDllSha256 = "944368b7a6eb0a97701e152a9171267db66377cc31bde415ab5da22c7a09cc43";

    [Fact]
    public void Hide_is_off_unless_the_user_turns_it_on()
    {
        Assert.False(new AppSettings().HideWinDivert);
        var loaded = JsonSerializer.Deserialize<AppSettings>("{}");
        Assert.NotNull(loaded);
        Assert.False(loaded.HideWinDivert);
    }

    [Fact]
    public void Swap_installs_the_monkey_pair_and_restore_puts_stock_back()
    {
        using var tree = new TempTree();
        var sets = CopySets(tree);
        var bin = tree.Combine("bin");
        Directory.CreateDirectory(bin);
        File.Copy(Path.Combine(sets, "stock", "WinDivert.dll"), Path.Combine(bin, "WinDivert.dll"));
        File.Copy(Path.Combine(sets, "stock", "WinDivert64.sys"), Path.Combine(bin, "WinDivert64.sys"));

        Assert.False(WinDivertHideService.NeedsApply(bin, sets, hide: false));

        WinDivertHideService.SwapBinaries(bin, sets, hide: true);

        Assert.False(File.Exists(Path.Combine(bin, "WinDivert64.sys")));
        Assert.True(WinDivertHideService.FileEquals(
            Path.Combine(bin, "Monkey64.sys"),
            Path.Combine(sets, "hide", "Monkey64.sys")));
        Assert.True(WinDivertHideService.FileEquals(
            Path.Combine(bin, "WinDivert.dll"),
            Path.Combine(sets, "hide", "WinDivert.dll")));
        Assert.False(WinDivertHideService.NeedsApply(bin, sets, hide: true));

        WinDivertHideService.SwapBinaries(bin, sets, hide: false);

        Assert.False(File.Exists(Path.Combine(bin, "Monkey64.sys")));
        Assert.True(WinDivertHideService.FileEquals(
            Path.Combine(bin, "WinDivert.dll"),
            Path.Combine(sets, "stock", "WinDivert.dll")));
        Assert.True(WinDivertHideService.FileEquals(
            Path.Combine(bin, "WinDivert64.sys"),
            Path.Combine(sets, "stock", "WinDivert64.sys")));
        Assert.False(WinDivertHideService.NeedsApply(bin, sets, hide: false));
    }

    [Fact]
    public void Leftover_monkey_file_next_to_a_stock_dll_is_removed_without_replacing_that_dll()
    {
        using var tree = new TempTree();
        var sets = CopySets(tree);
        var bin = tree.Combine("bin");
        Directory.CreateDirectory(bin);
        var dll = new byte[] { 1, 2, 3, 4 };
        var sys = new byte[] { 5, 6, 7 };
        File.WriteAllBytes(Path.Combine(bin, "WinDivert.dll"), dll);
        File.WriteAllBytes(Path.Combine(bin, "WinDivert64.sys"), sys);
        File.WriteAllBytes(Path.Combine(bin, "Monkey64.sys"), [9]);

        Assert.True(WinDivertHideService.NeedsApply(bin, sets, hide: false));
        WinDivertHideService.SwapBinaries(bin, sets, hide: false);

        Assert.Equal(dll, File.ReadAllBytes(Path.Combine(bin, "WinDivert.dll")));
        Assert.Equal(sys, File.ReadAllBytes(Path.Combine(bin, "WinDivert64.sys")));
        Assert.False(File.Exists(Path.Combine(bin, "Monkey64.sys")));
        Assert.False(WinDivertHideService.NeedsApply(bin, sets, hide: false));
    }

    [Fact]
    public void Health_check_accepts_monkey_and_still_reports_a_missing_driver()
    {
        using var tree = new TempTree();
        var root = tree.Combine("zapret");
        var bin = Path.Combine(root, "bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "winws.exe"), "exe");
        File.WriteAllText(Path.Combine(bin, "WinDivert.dll"), "dll");
        File.WriteAllText(Path.Combine(bin, "Monkey64.sys"), "sys");

        var hidden = ZapretHealth.MissingFiles(new ZapretPaths(root));
        Assert.DoesNotContain("WinDivert64.sys", hidden);
        Assert.DoesNotContain("Monkey64.sys", hidden);

        File.Delete(Path.Combine(bin, "Monkey64.sys"));
        var missing = ZapretHealth.MissingFiles(new ZapretPaths(root));
        Assert.Contains("WinDivert64.sys", missing);
    }

    [Fact]
    public void Sets_directory_is_the_windivert_folder_next_to_zapret()
    {
        using var tree = new TempTree();
        var zapret = tree.Combine("app", "zapret");
        Directory.CreateDirectory(Path.Combine(zapret, "bin"));
        File.WriteAllText(Path.Combine(zapret, "service.bat"), "@echo off");
        var sets = CopySets(tree, tree.Combine("app", "windivert"));

        Assert.Equal(Path.GetFullPath(sets), WinDivertHideService.ResolveSetsDirectory(zapret));
    }

    [Fact]
    public void Known_driver_services_include_windivert_variants_and_monkey()
    {
        Assert.Contains("WinDivert", ZapretShutdownService.DriverServiceNames);
        Assert.Contains("WinDivert14", ZapretShutdownService.DriverServiceNames);
        Assert.Contains("Monkey", ZapretShutdownService.DriverServiceNames);
        Assert.DoesNotContain(
            ZapretShutdownService.DriverServiceNames,
            name => name.Contains('a', StringComparison.Ordinal) && name.Contains("aaaa", StringComparison.Ordinal));
    }

    [Fact]
    public void Vendored_sets_match_the_bol_van_hide_package_and_the_stock_bin()
    {
        var root = TestPaths.RepoRoot();
        var sets = Path.Combine(root, "packaging", "windivert");
        var active = Path.Combine(root, "packaging", "zapret", "bin");

        Assert.True(WinDivertHideService.IsCompleteSetsDirectory(sets));
        Assert.Equal(StockDllSha256, Sha256(Path.Combine(sets, "stock", "WinDivert.dll")));
        Assert.Equal(StockSysSha256, Sha256(Path.Combine(sets, "stock", "WinDivert64.sys")));
        Assert.Equal(HideDllSha256, Sha256(Path.Combine(sets, "hide", "WinDivert.dll")));
        Assert.Equal(StockSysSha256, Sha256(Path.Combine(sets, "hide", "Monkey64.sys")));
        Assert.Equal(
            Sha256(Path.Combine(active, "WinDivert.dll")),
            Sha256(Path.Combine(sets, "stock", "WinDivert.dll")));
        Assert.Equal(
            Sha256(Path.Combine(active, "WinDivert64.sys")),
            Sha256(Path.Combine(sets, "stock", "WinDivert64.sys")));

        var hideDll = File.ReadAllBytes(Path.Combine(sets, "hide", "WinDivert.dll"));
        var stockDll = File.ReadAllBytes(Path.Combine(sets, "stock", "WinDivert.dll"));
        AssertContains(Utf16("\\Monkey64.sys"), hideDll);
        AssertContains(Utf16("Monkey"), hideDll);
        AssertDoesNotContain(Utf16("WinDivert64.sys"), hideDll);
        AssertDoesNotContain(Utf16("aaaaaaaaa"), hideDll);
        AssertContains(Utf16("\\WinDivert64.sys"), stockDll);
        AssertContains(Utf16("WinDivert"), stockDll);
        AssertDoesNotContain(Utf16("Monkey64.sys"), stockDll);

        var notes = File.ReadAllText(Path.Combine(sets, "README.md"));
        Assert.Contains("bol-van/zapret-win-bundle", notes, StringComparison.Ordinal);
        Assert.Contains("bb6a20af35ed74778eb669f5413990eb8209c711", notes, StringComparison.Ordinal);
        Assert.Contains("Flowseal", notes, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(sets, "upstream", "README.txt")));
        Assert.True(File.Exists(Path.Combine(sets, "upstream", "monkey_delete.cmd")));
        Assert.Contains("sc delete monkey", File.ReadAllText(Path.Combine(sets, "upstream", "monkey_delete.cmd")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Installer_ships_both_sets_and_diagnostics_know_monkey()
    {
        var root = TestPaths.RepoRoot();
        var build = File.ReadAllText(Path.Combine(root, "build-installer.ps1"));
        var csproj = File.ReadAllText(Path.Combine(root, "Aeroway.csproj"));
        var iss = File.ReadAllText(Path.Combine(root, "installer", "Aeroway.iss"));
        var bridge = File.ReadAllText(Path.Combine(root, "Scripts", "ui-bridge.ps1"));
        var service = File.ReadAllText(Path.Combine(root, "packaging", "zapret", "service.bat"));

        Assert.Contains("staging/windivert", build, StringComparison.Ordinal);
        Assert.Contains("hide\\Monkey64.sys", build, StringComparison.Ordinal);
        Assert.Contains("packaging\\windivert\\**\\*", csproj, StringComparison.Ordinal);
        Assert.Contains("{app}\\windivert", iss, StringComparison.Ordinal);
        Assert.Contains("[OK] WinDivert hide driver present (Monkey64.sys)", bridge, StringComparison.Ordinal);
        Assert.Contains("\"WinDivert\", \"WinDivert14\", \"Monkey\"", bridge, StringComparison.Ordinal);
        Assert.Contains("sc delete \"Monkey\"", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Both_languages_describe_the_toggle_and_the_limit()
    {
        var root = TestPaths.RepoRoot();
        var ru = File.ReadAllText(Path.Combine(root, "Resources", "Strings.ru.json"));
        var en = File.ReadAllText(Path.Combine(root, "Resources", "Strings.en.json"));
        foreach (var key in new[]
                 {
                     "service.windivert_hide",
                     "service.windivert_hide_hint",
                     "service.windivert_hide_on",
                     "service.windivert_hide_off",
                     "diag.windivert_hide_ok",
                     "diag.monkey_removed"
                 })
        {
            Assert.Contains($"\"{key}\"", ru, StringComparison.Ordinal);
            Assert.Contains($"\"{key}\"", en, StringComparison.Ordinal);
        }

        Assert.Contains("Скрыть WinDivert от античита", ru, StringComparison.Ordinal);
        Assert.Contains("Hide WinDivert from anticheat", en, StringComparison.Ordinal);
        Assert.Contains("имени устройства", ru, StringComparison.Ordinal);
        Assert.Contains("Device-name checks still see WinDivert", en, StringComparison.Ordinal);
    }

    private static string CopySets(TempTree tree, string? destination = null)
    {
        var source = Path.Combine(TestPaths.RepoRoot(), "packaging", "windivert");
        var target = destination ?? tree.Combine("windivert");
        foreach (var relative in new[]
                 {
                     Path.Combine("stock", "WinDivert.dll"),
                     Path.Combine("stock", "WinDivert64.sys"),
                     Path.Combine("hide", "WinDivert.dll"),
                     Path.Combine("hide", "Monkey64.sys")
                 })
        {
            var dest = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(Path.Combine(source, relative), dest, overwrite: true);
        }

        return target;
    }

    private static string Sha256(string path)
    {
        var hash = SHA256.HashData(File.ReadAllBytes(path));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[] Utf16(string text) => Encoding.Unicode.GetBytes(text);

    private static void AssertContains(byte[] needle, byte[] haystack) =>
        Assert.True(haystack.AsSpan().IndexOf(needle) >= 0, Encoding.Unicode.GetString(needle));

    private static void AssertDoesNotContain(byte[] needle, byte[] haystack) =>
        Assert.False(haystack.AsSpan().IndexOf(needle) >= 0, Encoding.Unicode.GetString(needle));
}
