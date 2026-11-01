namespace Aeroway.Services;

public sealed class ConsoleLog
{
    private static ConsoleLog? _instance;
    public static ConsoleLog Instance => _instance ??= new ConsoleLog();

    public event Action<string>? LineAdded;

    public void Write(string line)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {line}";
        LineAdded?.Invoke(timestamped);
    }

    public void Clear() => LineAdded?.Invoke("__CLEAR__");
}
