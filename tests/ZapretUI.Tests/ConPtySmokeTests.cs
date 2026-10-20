using System.Text;
using Xunit;
using ZapretUI.Services;

namespace ZapretUI.Tests;

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
            Assert.Contains("pty-ok", text.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            await host.DisposeAsync();
            Directory.Delete(directory, true);
        }
    }
}
