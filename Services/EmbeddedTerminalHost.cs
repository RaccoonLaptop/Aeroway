using System.IO;
using System.Text;

namespace ZapretUI.Services;

public sealed class EmbeddedTerminalHost : IAsyncDisposable
{
    private readonly Decoder _utf8 = Encoding.UTF8.GetDecoder();
    private ConPtyProcess? _connection;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    private int? _exitCode;

    public event Action<string>? OutputReceived;
    public event Action<int>? ProcessExited;
    public event Action<string>? ErrorOccurred;

    public bool IsRunning => _connection is not null;

    public async Task StartPowerShellScriptAsync(string scriptPath, string workingDirectory, CancellationToken ct = default)
    {
        await StopAsync();
        _utf8.Reset();

        var ps = ResolvePowerShellExecutable();
        _exitCode = null;
        _connection = SpawnWithFallback(ps, scriptPath, workingDirectory);
        _connection.Exited += code => _exitCode = code;

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
        await _connection.Input.WriteAsync(bytes, ct);
        await _connection.Input.FlushAsync(ct);
    }

    public async Task SendKeyAsync(char key, CancellationToken ct = default)
    {
        if (_connection is null) return;
        var bytes = Encoding.UTF8.GetBytes(key.ToString());
        await _connection.Input.WriteAsync(bytes, ct);
        await _connection.Input.FlushAsync(ct);
    }

    public async Task StopAsync()
    {
        _readCts?.Cancel();
        if (_connection is not null)
        {
            try { _connection.Kill(); }
            catch { /* ignore */ }
        }

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
                read = await _connection.Output.ReadAsync(buffer, ct);
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

            if (read <= 0)
            {
                for (var i = 0; i < 40 && _exitCode is null; i++)
                    await Task.Delay(50, ct).ConfigureAwait(false);
                ProcessExited?.Invoke(_exitCode ?? 0);
                _connection = null;
                break;
            }

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

    private static ConPtyProcess SpawnWithFallback(string ps, string scriptPath, string workingDirectory)
    {
        short[] cols = [300, 260, 200, 120];
        Exception? last = null;
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(BuildColorInitCommand(scriptPath)));
        var arguments = new[]
        {
            "-NoLogo",
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-EncodedCommand", encoded
        };

        foreach (var col in cols)
        {
            try
            {
                return ConPtyProcess.Start(ps, arguments, workingDirectory, col, 300);
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

    public async ValueTask DisposeAsync() => await StopAsync();

    private string DecodeUtf8(byte[] buffer, int read)
    {
        var maxChars = Encoding.UTF8.GetMaxCharCount(read);
        var chars = new char[maxChars];
        var count = _utf8.GetChars(buffer, 0, read, chars, 0, flush: false);
        return count == 0 ? "" : new string(chars, 0, count);
    }
}
