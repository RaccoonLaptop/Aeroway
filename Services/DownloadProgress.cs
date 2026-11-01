namespace Aeroway.Services;

public sealed class DownloadProgress
{
    public string Phase { get; init; } = "";
    public long BytesReceived { get; init; }
    public long? TotalBytes { get; init; }
    public double BytesPerSecond { get; init; }

    public double Percent =>
        TotalBytes is > 0 ? Math.Clamp(100.0 * BytesReceived / TotalBytes.Value, 0, 100) : -1;
}
