using System.IO.Pipes;
using System.Windows;

namespace ZapretUI.Services;

public sealed class SingleInstanceService : IDisposable
{
#if DEBUG
    private const string MutexName = "Global\\ZapretUI_SingleInstance-dev";
    private const string PipeName = "ZapretUI.ShowWindow-dev";
#else
    private const string MutexName = "Global\\ZapretUI_SingleInstance";
    private const string PipeName = "ZapretUI.ShowWindow";
#endif

    private readonly Mutex _mutex;
    private CancellationTokenSource? _listenCts;

    public bool IsFirstInstance { get; }

    private SingleInstanceService(Mutex mutex, bool isFirstInstance)
    {
        _mutex = mutex;
        IsFirstInstance = isFirstInstance;
    }

    public static SingleInstanceService Acquire(bool waitForRelease = false)
    {
        if (waitForRelease)
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var waitMutex = new Mutex(true, MutexName, out var acquired);
                if (acquired)
                {
                    var waited = new SingleInstanceService(waitMutex, true);
                    waited.StartListening();
                    return waited;
                }

                waitMutex.Dispose();
                Thread.Sleep(100);
            }
        }

        var mutex = new Mutex(true, MutexName, out var created);
        var service = new SingleInstanceService(mutex, created);
        if (created)
            service.StartListening();
        return service;
    }

    public static bool TryActivateExisting()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine("SHOW");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void StartListening()
    {
        _listenCts = new CancellationTokenSource();
        _ = ListenAsync(_listenCts.Token);
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                using var reader = new StreamReader(server);
                var command = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (!string.Equals(command, "SHOW", StringComparison.OrdinalIgnoreCase))
                    continue;

                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    if (Application.Current.MainWindow is MainWindow mw)
                        mw.ShowAndActivate();
                });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                try { await Task.Delay(400, ct).ConfigureAwait(false); } catch { break; }
            }
        }
    }

    public void Dispose()
    {
        _listenCts?.Cancel();
        _listenCts?.Dispose();
        try { _mutex.ReleaseMutex(); } catch { /* ignore */ }
        _mutex.Dispose();
    }
}
