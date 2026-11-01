using System.Diagnostics;
using System.Text;
using Aeroway.Helpers;

namespace Aeroway.Services;

public sealed class ProcessRunner
{
    private string? _zapretRoot;

    public event Action<string>? OutputReceived;

    public void SetZapretRoot(string root) => _zapretRoot = root;

    public async Task<int> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) Emit(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) Emit(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);
        return process.ExitCode;
    }

    public async Task<int> RunPowerShellAsync(string script, string? workingDirectory = null, CancellationToken ct = default)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return await RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            workingDirectory, ct);
    }

    public async Task<string> RunBridgeAsync(string action, string? extra = null, CancellationToken ct = default)
    {
        if (action == "RunTests")
        {
            RunInteractiveTest();
            return Loc.T("runner.test_window_open");
        }

        var scriptPath = ResolveBridgeScript();
        var root = _zapretRoot ?? ZapretPaths.DetectRoot();
        var needsAdmin = action is "InstallService" or "RemoveServices";

        var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Action \"{action}\" -Root \"{root}\"";
        if (!string.IsNullOrEmpty(extra))
            args += $" -Extra \"{extra.Replace("\"", "`\"")}\"";

        var captured = new StringBuilder();

        if (needsAdmin)
        {
            Emit(Loc.T("runner.uac_required"));
            var logFile = Path.Combine(Path.GetTempPath(), $"zapretui-bridge-{Guid.NewGuid():N}.log");
            args = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -Action \"{action}\" -Root \"{root}\" -LogFile \"{logFile}\"";
            if (!string.IsNullOrEmpty(extra))
                args += $" -Extra \"{extra.Replace("\"", "`\"")}\"";

            var psi = new ProcessStartInfo
            {
                FileName = ResolvePowerShellHost(),
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is not null)
                await process.WaitForExitAsync(ct);

            var adminCaptured = new StringBuilder();
            if (File.Exists(logFile))
            {
                foreach (var line in await File.ReadAllLinesAsync(logFile, ct))
                {
                    Emit(line);
                    adminCaptured.AppendLine(line);
                }
                try { File.Delete(logFile); } catch { /* ignore */ }
            }

            var adminResult = adminCaptured.Length > 0
                ? BridgeOutputFormatter.ForDialog(action, adminCaptured.ToString().TrimEnd())
                : Loc.F("runner.admin_action_done", action);
            if (process is not null && process.ExitCode != 0)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(adminResult) ? $"{Loc.T("common.error_prefix")} {action}" : adminResult);
            return adminResult;
        }

        void Capture(string line) => captured.AppendLine(line);

        OutputReceived += Capture;
        int exitCode;
        try
        {
            exitCode = await RunAsync("powershell.exe", args, root, ct);
        }
        finally
        {
            OutputReceived -= Capture;
        }

        var result = captured.Length > 0
            ? BridgeOutputFormatter.ForDialog(action, captured.ToString().TrimEnd())
            : Loc.T("runner.operation_done");
        if (exitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result) ? $"{Loc.T("common.error_prefix")} {action}" : result);

        return result;
    }

    public Task RunInteractiveTestAsync(CancellationToken ct = default)
    {
        RunInteractiveTest();
        return Task.CompletedTask;
    }

    private void RunInteractiveTest()
    {
        var root = _zapretRoot ?? ZapretPaths.DetectRoot();
        var testScript = ResolveTestScript(root);
        if (testScript is null)
            throw new FileNotFoundException(Loc.T("runner.script_missing"));

        Emit(Loc.T("runner.test_opening"));
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -NoExit -File \"{testScript}\"",
            WorkingDirectory = root,
            UseShellExecute = true
        });
    }

    public static string? ResolveTestScript(string root)
    {
        var utils = Path.Combine(root, "utils");
        if (!Directory.Exists(utils)) return null;

        var exact = Path.Combine(utils, "test zapret.ps1");
        if (File.Exists(exact)) return exact;

        foreach (var file in Directory.GetFiles(utils, "test*.ps1"))
        {
            if (file.Contains("test", StringComparison.OrdinalIgnoreCase))
                return file;
        }

        return null;
    }

    private static string ResolveBridgeScript() =>
        TryResolveBridgeScript(AppContext.BaseDirectory, ZapretPaths.DetectRoot())
        ?? throw new FileNotFoundException("ui-bridge.ps1 not found");

    internal static string? TryResolveBridgeScript(string baseDirectory, string zapretRoot)
    {
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "Scripts", "ui-bridge.ps1"),
            Path.Combine(baseDirectory, "..", "..", "..", "Scripts", "ui-bridge.ps1"),
            Path.Combine(baseDirectory, "..", "Aeroway", "Scripts", "ui-bridge.ps1"),
            Path.Combine(baseDirectory, "..", "ZapretUI", "Scripts", "ui-bridge.ps1"),
            Path.Combine(zapretRoot, "Aeroway-Program", "Scripts", "ui-bridge.ps1"),
            Path.Combine(zapretRoot, "ZapretUI-Program", "Scripts", "ui-bridge.ps1"),
            Path.Combine(zapretRoot, "Aeroway", "Scripts", "ui-bridge.ps1"),
            Path.Combine(zapretRoot, "ZapretUI", "Scripts", "ui-bridge.ps1")
        };

        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (File.Exists(full)) return full;
        }

        return null;
    }

    private static string ResolvePowerShellHost()
    {
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var pwsh = Path.Combine(windir, "System32", "WindowsPowerShell", "v1.0", "powershellw.exe");
        return File.Exists(pwsh) ? pwsh : "powershell.exe";
    }

    private void Emit(string line) => OutputReceived?.Invoke(line);
}
