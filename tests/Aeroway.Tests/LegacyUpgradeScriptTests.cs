using System.Diagnostics;
using Xunit;

namespace Aeroway.Tests;

public class LegacyUpgradeScriptTests
{
    [Theory]
    [InlineData("apply-update.ps1", "-SourceDir")]
    [InlineData("apply-update-installer.ps1", "-InstallerPath")]
    public void Updater_launches_zapretui_exe_when_aeroway_exe_is_missing(string scriptName, string firstArg)
    {
        var script = Path.Combine(TestPaths.RepoRoot(), "Scripts", scriptName);
        var text = File.ReadAllText(script);
        Assert.Contains("Get-Process -Name 'ZapretUI','Aeroway'", text, StringComparison.Ordinal);
        if (scriptName == "apply-update.ps1")
            Assert.Contains("ZapretUI-install-payload", text, StringComparison.Ordinal);

        using var tree = new TempTree();
        var target = tree.Combine("install");
        Directory.CreateDirectory(target);

        var missing = RunLaunchResolve(script, firstArg, target);
        Assert.True(missing.ExitCode == 2, missing.Stdout + missing.Stderr);

        File.WriteAllText(Path.Combine(target, "ZapretUI.exe"), "old");
        var legacy = RunLaunchResolve(script, firstArg, target);
        Assert.True(legacy.ExitCode == 0, legacy.Stdout + legacy.Stderr);
        Assert.Equal(Path.Combine(target, "ZapretUI.exe"), legacy.Stdout.Trim());

        File.WriteAllText(Path.Combine(target, "Aeroway.exe"), "new");
        var current = RunLaunchResolve(script, firstArg, target);
        Assert.True(current.ExitCode == 0, current.Stdout + current.Stderr);
        Assert.Equal(Path.Combine(target, "Aeroway.exe"), current.Stdout.Trim());
    }

    [Fact]
    public void Remove_legacy_script_deletes_only_a_zapretui_folder_and_old_update_scratch()
    {
        var script = Path.Combine(TestPaths.RepoRoot(), "Scripts", "remove-legacy-zapretui.ps1");
        using var tree = new TempTree();
        var legacy = tree.Combine("ZapretUI");
        var other = tree.Combine("Aeroway");
        Directory.CreateDirectory(legacy);
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(legacy, "marker.txt"), "old");
        File.WriteAllText(Path.Combine(other, "marker.txt"), "keep");

        var scratch = new[]
        {
            Path.Combine(Path.GetTempPath(), "ZapretUI-update-" + Guid.NewGuid().ToString("N")),
            Path.Combine(Path.GetTempPath(), "ZapretUI-backup-" + Guid.NewGuid().ToString("N")),
            Path.Combine(Path.GetTempPath(), "Aeroway-update-" + Guid.NewGuid().ToString("N")),
            Path.Combine(Path.GetTempPath(), "Aeroway-backup-" + Guid.NewGuid().ToString("N"))
        };
        try
        {
            foreach (var dir in scratch)
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "payload.txt"), "temp");
            }

            var refused = Run(script, "-LegacyPath", other, "-WaitPid", "0");
            Assert.Equal(1, refused.ExitCode);
            Assert.True(File.Exists(Path.Combine(other, "marker.txt")));
            Assert.True(File.Exists(Path.Combine(legacy, "marker.txt")));

            var removed = Run(script, "-LegacyPath", legacy, "-WaitPid", "0");
            Assert.Equal(0, removed.ExitCode);
            Assert.False(Directory.Exists(legacy));
            Assert.True(File.Exists(Path.Combine(other, "marker.txt")));
            Assert.All(scratch, dir => Assert.False(Directory.Exists(dir)));
        }
        finally
        {
            foreach (var dir in scratch)
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }
    }

    private static ScriptResult RunLaunchResolve(string script, string firstArg, string target)
    {
        var firstValue = firstArg == "-InstallerPath"
            ? Path.Combine(target, "Aeroway-Setup.exe")
            : target;
        return Run(
            script,
            firstArg, firstValue,
            "-TargetDir", target,
            "-ProcessId", "0",
            "-ExePath", Path.Combine(target, "Aeroway.exe"),
            "-ResolveLaunchOnly");
    }

    private static ScriptResult Run(string script, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.Unicode,
            StandardErrorEncoding = System.Text.Encoding.Unicode,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(script);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi);
        Assert.NotNull(process);
        var stdout = process!.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(60000), "PowerShell did not exit: " + stderr);
        return new ScriptResult(process.ExitCode, stdout, stderr);
    }

    private sealed record ScriptResult(int ExitCode, string Stdout, string Stderr);
}
