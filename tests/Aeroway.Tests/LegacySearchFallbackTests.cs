using Aeroway.Services;
using Xunit;

namespace Aeroway.Tests;

public class LegacySearchFallbackTests
{
    [Fact]
    public void Local_manifest_search_checks_zapretui_update_after_the_renamed_paths()
    {
        var install = Path.Combine("C:\\Aeroway");
        var zapret = Path.Combine(install, "zapret");
        var paths = AppSelfUpdateService.EnumerateLocalManifestPaths(install, zapret).ToArray();

        Assert.Equal(
            new[]
            {
                Path.Combine(install, "update.json"),
                Path.Combine(zapret, "Aeroway-update", "update.json"),
                Path.Combine(zapret, "ZapretUI-update", "update.json")
            },
            paths);

        using var tree = new TempTree();
        var root = tree.Combine("zapret");
        Directory.CreateDirectory(Path.Combine(root, "ZapretUI-update"));
        File.WriteAllText(Path.Combine(root, "ZapretUI-update", "update.json"), "{\"version\":\"2.1.16\"}");
        var found = AppSelfUpdateService.EnumerateLocalManifestPaths(tree.Combine("install"), root)
            .FirstOrDefault(File.Exists);
        Assert.Equal(Path.Combine(root, "ZapretUI-update", "update.json"), found);
    }

    [Theory]
    [InlineData("Aeroway.exe")]
    [InlineData("ZapretUI.exe")]
    public void Update_package_root_accepts_either_executable_name(string exeName)
    {
        using var tree = new TempTree();
        var nested = tree.Combine("extract", "payload");
        Directory.CreateDirectory(nested);
        Assert.False(AppSelfUpdateService.HasProgramExe(nested));
        Assert.Null(AppSelfUpdateService.FindProgramRoot(tree.Combine("extract")));

        File.WriteAllText(Path.Combine(nested, exeName), "exe");
        Assert.True(AppSelfUpdateService.HasProgramExe(nested));
        Assert.Equal(nested, AppSelfUpdateService.FindProgramRoot(tree.Combine("extract")));

        File.WriteAllText(Path.Combine(tree.Combine("extract"), "Aeroway.exe"), "outer");
        Assert.Equal(tree.Combine("extract"), AppSelfUpdateService.FindProgramRoot(tree.Combine("extract")));
    }

    [Fact]
    public void Installed_exe_falls_back_to_the_zapretui_copy()
    {
        using var tree = new TempTree();
        var dir = tree.Combine("app");
        Directory.CreateDirectory(dir);
        Assert.Equal(Path.Combine(dir, "ZapretUI.exe"), AppSelfUpdateService.ResolveInstalledExe(dir));

        File.WriteAllText(Path.Combine(dir, "ZapretUI.exe"), "old");
        Assert.Equal(Path.Combine(dir, "ZapretUI.exe"), AppSelfUpdateService.ResolveInstalledExe(dir));
        File.WriteAllText(Path.Combine(dir, "Aeroway.exe"), "new");
        Assert.Equal(Path.Combine(dir, "Aeroway.exe"), AppSelfUpdateService.ResolveInstalledExe(dir));
    }

    [Fact]
    public void Update_scripts_are_found_beside_the_executable_or_under_scripts()
    {
        using var tree = new TempTree();
        var baseDir = tree.Combine("app");
        Directory.CreateDirectory(baseDir);
        Assert.Null(AppSelfUpdateService.ResolveScript(baseDir, "apply-update.ps1"));

        File.WriteAllText(Path.Combine(baseDir, "apply-update.ps1"), "loose");
        Assert.Equal(Path.Combine(baseDir, "apply-update.ps1"), AppSelfUpdateService.ResolveScript(baseDir, "apply-update.ps1"));

        Directory.CreateDirectory(Path.Combine(baseDir, "Scripts"));
        File.WriteAllText(Path.Combine(baseDir, "Scripts", "apply-update.ps1"), "scripts");
        Assert.Equal(
            Path.Combine(baseDir, "Scripts", "apply-update.ps1"),
            AppSelfUpdateService.ResolveScript(baseDir, "apply-update.ps1"));
    }

    [Fact]
    public void Bridge_script_search_still_finds_old_zapretui_trees()
    {
        using var tree = new TempTree();
        // Three parents above the app folder stay inside the temp tree.
        var baseDir = tree.Combine("a", "b", "c");
        var zapret = tree.Combine("zapret");
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(zapret);

        Assert.Null(ProcessRunner.TryResolveBridgeScript(baseDir, zapret));

        var legacyLocations = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Scripts", "ui-bridge.ps1")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "Aeroway", "Scripts", "ui-bridge.ps1")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "ZapretUI", "Scripts", "ui-bridge.ps1")),
            Path.GetFullPath(Path.Combine(zapret, "Aeroway-Program", "Scripts", "ui-bridge.ps1")),
            Path.GetFullPath(Path.Combine(zapret, "ZapretUI-Program", "Scripts", "ui-bridge.ps1")),
            Path.GetFullPath(Path.Combine(zapret, "Aeroway", "Scripts", "ui-bridge.ps1")),
            Path.GetFullPath(Path.Combine(zapret, "ZapretUI", "Scripts", "ui-bridge.ps1"))
        };

        foreach (var location in legacyLocations)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(location)!);
            File.WriteAllText(location, "bridge");
            Assert.Equal(location, ProcessRunner.TryResolveBridgeScript(baseDir, zapret));
            File.Delete(location);
        }

        var preferred = Path.Combine(baseDir, "Scripts", "ui-bridge.ps1");
        var legacy = Path.GetFullPath(Path.Combine(baseDir, "..", "ZapretUI", "Scripts", "ui-bridge.ps1"));
        Directory.CreateDirectory(Path.GetDirectoryName(preferred)!);
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(preferred, "bridge");
        File.WriteAllText(legacy, "legacy");
        Assert.Equal(Path.GetFullPath(preferred), ProcessRunner.TryResolveBridgeScript(baseDir, zapret));
    }

    [Fact]
    public void Test_script_search_prefers_the_flowseal_name_then_any_test_script()
    {
        using var tree = new TempTree();
        var root = tree.Combine("zapret");
        Assert.Null(ProcessRunner.ResolveTestScript(root));

        var utils = Path.Combine(root, "utils");
        Directory.CreateDirectory(utils);
        File.WriteAllText(Path.Combine(utils, "test-extra.ps1"), "extra");
        Assert.Equal(Path.Combine(utils, "test-extra.ps1"), ProcessRunner.ResolveTestScript(root));

        var exact = Path.Combine(utils, "test zapret.ps1");
        File.WriteAllText(exact, "exact");
        Assert.Equal(exact, ProcessRunner.ResolveTestScript(root));
    }
}
