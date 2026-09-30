using System.Net.NetworkInformation;
using ZapretUI.Controls.Backgrounds;

namespace ZapretUI.Services;

/// <summary>
/// Считает скорость сети в фоне, чтобы кадр волн не останавливался.
/// </summary>
public static class NetFlow
{
    private static Timer? _timer;
    private static int _started;
    private static NetworkInterface[] _nics = [];
    private static int _refresh;
    private static long _lastBytes;
    private static long _lastTicks;
    private static bool _ready;

    public static void EnsureRunning()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;
        _timer = new Timer(static _ => Sample(), null, 0, 250);
    }

    private static void Sample()
    {
        try
        {
            if (_nics.Length == 0 || ++_refresh % 20 == 0)
                _nics = ListNics();

            long bytes = 0;
            foreach (var nic in _nics)
            {
                try
                {
                    var stats = nic.GetIPStatistics();
                    bytes += stats.BytesReceived + stats.BytesSent;
                }
                catch
                {
                    /* этот адаптер пропускаем */
                }
            }

            var now = Environment.TickCount64;
            if (!_ready)
            {
                _ready = true;
                _lastBytes = bytes;
                _lastTicks = now;
                return;
            }

            var seconds = (now - _lastTicks) / 1000d;
            _lastTicks = now;
            if (seconds < 0.05)
                return;

            var delta = bytes - _lastBytes;
            _lastBytes = bytes;
            if (delta < 0)
                delta = 0;

            var perSecond = delta / seconds;
            WaveSignal.Flow = 1d - Math.Exp(-perSecond / 80_000d);
            FlowTrace.Push(perSecond);
        }
        catch
        {
            /* счётчик сети недоступен — волна просто остаётся низкой */
        }
    }

    private static NetworkInterface[] ListNics()
    {
        var list = new List<NetworkInterface>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;
            list.Add(nic);
        }
        return list.ToArray();
    }
}
