using System.IO;
using System.Text;
using Porta.Pty;

namespace ZapretUI.Services;

public sealed class EmbeddedTerminalHost : IAsyncDisposable
{
    private readonly Decoder _utf8 = Encoding.UTF8.GetDecoder();
    private IPtyConnection? _connection;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    public event Action<string>? OutputReceived;
    public event Action<int>? ProcessExited;
    public event Action<string>? ErrorOccurred;

    public bool IsRunning => _connection is not null;

    public async Task StartPowerShellScriptAsync(string scriptPath, string workingDirectory, CancellationToken ct = default)
    {
        await StopAsync();
        _utf8.Reset();

        var ps = ResolvePowerShellExecutable();
        _connection = await SpawnWithFallbackAsync(ps, scriptPath, workingDirectory, ct);
        _connection.ProcessExited += (_, e) =>
        {
            ProcessExited?.Invoke(e.ExitCode);
            _connection = null;
        };

        _readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _readTask = Task.Run(() => ReadLoopAsync(_readCts.Token), _readCts.Token);
    }

    public async Task WriteInputAsync(string text, CancellationToken ct = default)
    {
        if (_connection is null) return;
        var payload = string.IsNullOrEmpty(text)
            ? "\r"
            : (text.EndsWith('\r') ? text : text + "\r");
        var bytes = Encoding.UTF8.GetBytes(payload);
        await _connection.WriterStream.WriteAsync(bytes, ct);
        await _connection.WriterStream.FlushAsync(ct);
    }

    public async Task SendKeyAsync(char key, CancellationToken ct = default)
    {
        if (_connection is null) return;
        var bytes = Encoding.UTF8.GetBytes(key.ToString());
        await _connection.WriterStream.WriteAsync(bytes, ct);
        await _connection.WriterStream.FlushAsync(ct);
    }

    public async Task StopAsync()
    {
        _readCts?.Cancel();
        if (_readTask is not null)
        {
            try { await _readTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            _readTask = null;
        }
        _readCts?.Dispose();
        _readCts = null;

        if (_connection is not null)
        {
            try { _connection.Kill(); }
            catch { /* ignore */ }
            _connection.Dispose();
            _connection = null;
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        while (!ct.IsCancellationRequested && _connection is not null)
        {
            int read;
            try
            {
                read = await _connection.ReaderStream.ReadAsync(buffer, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex.Message);
                break;
            }

            if (read <= 0) break;

            var chunk = DecodeUtf8(buffer, read);
            if (!string.IsNullOrEmpty(chunk))
            {
                try
                {
                    OutputReceived?.Invoke(chunk);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(ex.Message);
                }
            }
        }
    }

    private static async Task<IPtyConnection> SpawnWithFallbackAsync(
        string ps,
        string scriptPath,
        string workingDirectory,
        CancellationToken ct)
    {
        var cols = new[] { 300, 260, 200, 120 };
        Exception? last = null;

        foreach (var col in cols)
        {
            try
            {
                return await PtyProvider.SpawnAsync(CreateOptions(ps, scriptPath, workingDirectory, col), ct);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw last ?? new InvalidOperationException("Failed to start terminal");
    }

    private static string ResolvePowerShellExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PowerShell", "7", "pwsh.exe"),
            Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return "powershell.exe";
    }

    private static string BuildColorInitCommand(string scriptPath)
    {
        var escapedScript = scriptPath.Replace("'", "''");
        return
            "$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); " +
            "if ($PSVersionTable.PSVersion.Major -ge 7) { $PSStyle.OutputRendering = 'Ansi' }; " +
            $"& '{escapedScript}'";
    }

    private static PtyOptions CreateOptions(string ps, string scriptPath, string workingDirectory, int cols) =>
        new()
        {
            Name = "ZapretTest",
            Cols = cols,
            Rows = 300,
            Cwd = workingDirectory,
            App = ps,
            CommandLine =
            [
                "-NoLogo",
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-Command", BuildColorInitCommand(scriptPath)
            ]
        };

    public async ValueTask DisposeAsync() => await StopAsync();

    private string DecodeUtf8(byte[] buffer, int read)
    {
        var maxChars = Encoding.UTF8.GetMaxCharCount(read);
        var chars = new char[maxChars];
        var count = _utf8.GetChars(buffer, 0, read, chars, 0, flush: false);
        return count == 0 ? "" : new string(chars, 0, count);
    }
}
