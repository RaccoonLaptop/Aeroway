using System.Diagnostics;
using System.Text;
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
        Assert.Equal(Path.Combine(target, "ZapretUI.exe"), legacy.Stdout);

        File.WriteAllText(Path.Combine(target, "Aeroway.exe"), "new");
        var current = RunLaunchResolve(script, firstArg, target);
        Assert.True(current.ExitCode == 0, current.Stdout + current.Stderr);
        Assert.Equal(Path.Combine(target, "Aeroway.exe"), current.Stdout);
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
        // Windows PowerShell 5.1 writes this path as ASCII (or UTF-8). Reading the
        // pipe as UTF-16 turns "C:" into U+3A43 (㩃). Capture bytes 1:1, then decode.
        var byteEncoding = Encoding.Latin1;
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = byteEncoding,
            StandardErrorEncoding = byteEncoding,
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
        var stdout = DecodePowerShellText(process!.StandardOutput.ReadToEnd(), byteEncoding);
        var stderr = DecodePowerShellText(process.StandardError.ReadToEnd(), byteEncoding);
        Assert.True(process.WaitForExit(60000), "PowerShell did not exit: " + stderr);
        return new ScriptResult(process.ExitCode, stdout, stderr);
    }

    private static string DecodePowerShellText(string captured, Encoding byteEncoding)
    {
        var bytes = byteEncoding.GetBytes(captured);
        if (bytes.Length == 0)
            return "";

        Encoding encoding;
        var start = 0;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encoding = Encoding.Unicode;
            start = 2;
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encoding = Encoding.BigEndianUnicode;
            start = 2;
        }
        else if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encoding = Encoding.UTF8;
            start = 3;
        }
        else if (LooksLikeUtf16Le(bytes))
        {
            encoding = Encoding.Unicode;
        }
        else
        {
            encoding = Encoding.UTF8;
        }

        return encoding.GetString(bytes, start, bytes.Length - start).Trim().Trim('\uFEFF');
    }

    private static bool LooksLikeUtf16Le(byte[] bytes)
    {
        if (bytes.Length < 4 || bytes.Length % 2 != 0)
            return false;

        var zeroHighBytes = 0;
        var pairs = bytes.Length / 2;
        for (var i = 1; i < bytes.Length; i += 2)
        {
            if (bytes[i] == 0)
                zeroHighBytes++;
        }

        return zeroHighBytes == pairs;
    }

    private sealed record ScriptResult(int ExitCode, string Stdout, string Stderr);
}
