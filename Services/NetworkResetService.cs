using System.Diagnostics;
using System.IO;
using System.Text;
using Aeroway.Helpers;

namespace Aeroway.Services;

public sealed class NetworkResetService
{
    public static async Task<(bool Success, string Output)> RunAllAsync(CancellationToken ct = default)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "zapret-netreset.log");
        try { if (File.Exists(logPath)) File.Delete(logPath); } catch { /* ignore */ }

        var script = BuildScript(logPath);
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi);
            if (process is null)
                return (false, Loc.T("network.admin_required"));

            await process.WaitForExitAsync(ct);

            if (File.Exists(logPath))
            {
                var text = await File.ReadAllTextAsync(logPath, ct);
                return (process.ExitCode == 0, string.IsNullOrWhiteSpace(text)
                    ? Loc.T("network.reset_ok")
                    : text.TrimEnd());
            }

            return (process.ExitCode == 0, Loc.T("network.reset_ok"));
        }
        catch (Exception ex)
        {
            return (false, $"{Loc.T("common.error_prefix")} {ex.Message}");
        }
    }

    private static string BuildScript(string logPath)
    {
        var escapedLog = logPath.Replace("'", "''");
        var steps = new[]
        {
            (Loc.T("network.step_ip"), "netsh int ip reset"),
            (Loc.T("network.step_winhttp"), "netsh winhttp reset proxy"),
            (Loc.T("network.step_winsock"), "netsh winsock reset"),
            (Loc.T("network.step_ipv4"), "netsh interface ipv4 reset"),
            (Loc.T("network.step_ipv6"), "netsh interface ipv6 reset"),
            (Loc.T("network.step_tcp"), "netsh int ipv4 set dynamicport tcp start=10000 num=30000"),
            (Loc.T("network.step_dns"), "ipconfig /flushdns")
        };

        var stepLines = string.Join(",\n    ", steps.Select(s =>
            $"@{{ L='{s.Item1.Replace("'", "''")}'; C='{s.Item2}' }}"));

        return $@"
$ErrorActionPreference = 'Continue'
$log = New-Object System.Text.StringBuilder
$steps = @(
    {stepLines}
)
foreach ($s in $steps) {{
    [void]$log.AppendLine(""=== $($s.L) ==="")
    [void]$log.AppendLine(""> $($s.C)"")
    $out = cmd /c $($s.C) 2>&1 | Out-String
    if ($out.Trim()) {{ [void]$log.AppendLine($out.TrimEnd()) }} else {{ [void]$log.AppendLine('OK') }}
    [void]$log.AppendLine()
}}
[void]$log.AppendLine('{Loc.T("network.log_done").Replace("'", "''")}')
$text = $log.ToString()
Set-Content -Path '{escapedLog}' -Value $text -Encoding UTF8
Write-Output $text
";
    }
}
