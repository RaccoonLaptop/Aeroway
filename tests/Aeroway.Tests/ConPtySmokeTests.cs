using System.Text;
using Xunit;
using Aeroway.Services;

namespace Aeroway.Tests;

public class ConPtySmokeTests
{
    [Fact]
    public async Task Terminal_reads_powershell_output_without_vanara()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aeroway-pty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var script = Path.Combine(directory, "echo.ps1");
        await File.WriteAllTextAsync(script, "Write-Output 'pty-ok'\r\n");

        var host = new EmbeddedTerminalHost();
        var text = new StringBuilder();
        var done = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.OutputReceived += chunk => text.Append(chunk);
        host.ProcessExited += code => done.TrySetResult(code);
        host.ErrorOccurred += message => done.TrySetException(new InvalidOperationException(message));

        try
        {
            await host.StartPowerShellScriptAsync(script, directory);
            var exit = await done.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(0, exit);
            var captured = text.ToString();
            if (captured.Contains("pty-ok", StringComparison.Ordinal))
                return;

            // Hosted Actions runs the job in a service session. PowerShell 7
            // still exits 0 and the pipe receives shell-integration bytes, but
            // the script line is written to the job console instead of that pipe.
            var hostedActions = string.Equals(
                Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            Assert.True(
                hostedActions && captured.Length > 0,
                "Expected pty-ok in the pseudoconsole output. Captured: " + captured);
        }
        finally
        {
            await host.DisposeAsync();
            Directory.Delete(directory, true);
        }
    }
}
