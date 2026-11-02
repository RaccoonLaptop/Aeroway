using Aeroway.Services;
using Xunit;

namespace Aeroway.Tests;

public class PackagingLayoutTests
{
    [Fact]
    public void Strategies_folder_keeps_the_bat_and_drops_the_unused_universal_config()
    {
        var strategies = Path.Combine(TestPaths.RepoRoot(), "packaging", "strategies");
        Assert.True(File.Exists(Path.Combine(strategies, "Niko_ALT11.bat")));
        Assert.False(File.Exists(Path.Combine(strategies, "Niko_ALT11.json")));
    }

    [Fact]
    public void Installer_publish_does_not_copy_the_zapret_engine_twice()
    {
        var root = TestPaths.RepoRoot();
        var csproj = File.ReadAllText(Path.Combine(root, "Aeroway.csproj"));
        var build = File.ReadAllText(Path.Combine(root, "build-installer.ps1"));
        Assert.Contains("AerowaySkipBundledZapretCopy", csproj, StringComparison.Ordinal);
        Assert.Contains("-p:AerowaySkipBundledZapretCopy=true", build, StringComparison.Ordinal);
        Assert.Contains("Join-Path $StagingDir \"zapret\"", build, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_still_removes_zapret_ui_shortcuts()
    {
        var iss = File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), "installer", "Aeroway.iss"));
        Assert.Contains("{autoprograms}\\Zapret UI.lnk", iss, StringComparison.Ordinal);
        Assert.Contains("{autodesktop}\\Zapret UI.lnk", iss, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_manifests_stay_paired()
    {
        var root = TestPaths.RepoRoot();
        var repo = File.ReadAllText(Path.Combine(root, "update.json")).Replace("\r\n", "\n").Trim();
        var packaging = File.ReadAllText(Path.Combine(root, "packaging", "update.json")).Replace("\r\n", "\n").Trim();
        Assert.Equal(repo, packaging);
        // The number itself moves when the release workflow points the manifests
        // at the installer it just published. Pin the shape, not one version.
        Assert.Matches("\"version\":\\s+\"\\d+\\.\\d+\\.\\d+\"", repo);
        Assert.Contains(
            "https://github.com/RaccoonLaptop/Aeroway/releases/download/v",
            repo,
            StringComparison.Ordinal);
        Assert.Contains("/Aeroway-Setup.exe", repo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tagged_release_publishes_the_debug_portable_zip_beside_the_installer()
    {
        var root = TestPaths.RepoRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        var script = File.ReadAllText(Path.Combine(root, "build-dev-portable.ps1"));

        Assert.Contains("Tag matches project version", workflow, StringComparison.Ordinal);
        Assert.Contains("does not match Aeroway.csproj", workflow, StringComparison.Ordinal);
        Assert.Contains("dist\\Aeroway-Setup.exe", workflow, StringComparison.Ordinal);
        Assert.Contains("dist\\Aeroway-Dev.zip", workflow, StringComparison.Ordinal);
        Assert.Contains("build-dev-portable.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("-c Debug", script, StringComparison.Ordinal);
        Assert.Contains("win-x64", script, StringComparison.Ordinal);
        Assert.Contains("--self-contained true", script, StringComparison.Ordinal);
        Assert.Contains("Aeroway-Dev.exe", script, StringComparison.Ordinal);
        Assert.Contains("Aeroway-Dev.zip", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Bundled_engine_uses_packaging_zapret_then_the_installed_zapret_folder()
    {
        using var tree = new TempTree();
        var packaged = tree.Combine("packaging", "zapret");
        var installed = tree.Combine("zapret");
        WriteEngine(installed, "installed");
        Assert.Equal(installed, Path.GetFullPath(BundledZapretService.ResolveBundledDirectory(tree.Root)));

        WriteEngine(packaged, "packaged");
        Assert.Equal(packaged, Path.GetFullPath(BundledZapretService.ResolveBundledDirectory(tree.Root)));
    }

    private static void WriteEngine(string root, string version)
    {
        Directory.CreateDirectory(Path.Combine(root, "bin"));
        Directory.CreateDirectory(Path.Combine(root, ".service"));
        File.WriteAllText(Path.Combine(root, "service.bat"), "@echo off");
        File.WriteAllText(Path.Combine(root, ".service", "version.txt"), version);
    }
}
