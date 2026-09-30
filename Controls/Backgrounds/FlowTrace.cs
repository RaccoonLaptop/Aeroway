namespace ZapretUI.Controls.Backgrounds;

public readonly struct FlowSample
{
    public long TickMs { get; init; }
    public double BytesPerSecond { get; init; }
}

/// <summary>
/// Последняя минута скорости. Пишет таймер сети, читает кадр диаграммы.
/// </summary>
public static class FlowTrace
{
    public const int Capacity = 256;
    public const int WindowMs = 60_000;

    private static readonly FlowSample[] Buffer = new FlowSample[Capacity];
    private static readonly object Gate = new();
    private static int _count;
    private static int _next;

    public static void Push(double bytesPerSecond) =>
        Push(Environment.TickCount64, bytesPerSecond);

    public static void Push(long tickMs, double bytesPerSecond)
    {
        var sample = new FlowSample
        {
            TickMs = tickMs,
            BytesPerSecond = Math.Max(0, bytesPerSecond)
        };
        lock (Gate)
        {
            Buffer[_next] = sample;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity)
                _count++;
        }
    }

    public static int Copy(FlowSample[] destination, out long now)
    {
        now = Environment.TickCount64;
        lock (Gate)
        {
            var count = Math.Min(_count, destination.Length);
            var start = (_next - count + Capacity) % Capacity;
            for (var i = 0; i < count; i++)
                destination[i] = Buffer[(start + i) % Capacity];
            return count;
        }
    }
}
