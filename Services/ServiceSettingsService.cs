using System.IO;
using System.Text.RegularExpressions;

namespace Aeroway.Services;

public readonly record struct GameFilterState(string Mode, string TcpRange, string UdpRange)
{
    public const string DefaultRange = "1024-65535";
    public const string DisabledPort = "12";

    public static GameFilterState DisabledDefault { get; } =
        new("disabled", DefaultRange, DefaultRange);

    public bool IsTcpEnabled => Mode is "all" or "tcp";
    public bool IsUdpEnabled => Mode is "all" or "udp";

    public string ResolvedTcp => IsTcpEnabled ? TcpRange : DisabledPort;
    public string ResolvedUdp => IsUdpEnabled ? UdpRange : DisabledPort;
    public string ResolvedFilter => IsTcpEnabled ? TcpRange : IsUdpEnabled ? UdpRange : DisabledPort;
}

public sealed class ServiceSettingsService
{
    private static readonly Regex PortItemRegex = new(
        @"^[1-9]\d*(?:-[1-9]\d*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ZapretPaths _paths;

    public ServiceSettingsService(ZapretPaths paths) => _paths = paths;

    public GameFilterState GetGameFilter() => ReadGameFilter(_paths.Utils);

    public string GetGameFilterMode() => GetGameFilter().Mode;

    public string GetGameFilterStatus()
    {
        return GetGameFilterMode() switch
        {
            "disabled" => "disabled",
            "all" => "enabled (TCP and UDP)",
            "tcp" => "enabled (TCP)",
            "udp" => "enabled (UDP)",
            _ => "enabled"
        };
    }

    public void SetGameFilter(string mode)
    {
        var current = GetGameFilter();
        WriteGameFilter(current with { Mode = NormalizeMode(mode) });
    }

    public void SetGameFilterPorts(string tcpRange, string udpRange)
    {
        if (!TryNormalizePortRange(tcpRange, out var tcp))
            throw new ArgumentException("Invalid TCP Game Filter port range.", nameof(tcpRange));
        if (!TryNormalizePortRange(udpRange, out var udp))
            throw new ArgumentException("Invalid UDP Game Filter port range.", nameof(udpRange));

        var current = GetGameFilter();
        WriteGameFilter(current with { TcpRange = tcp, UdpRange = udp });
    }

    public static GameFilterState ReadGameFilter(string utilsDir)
    {
        var flag = Path.Combine(utilsDir, "game_filter.enabled");
        if (!File.Exists(flag))
            return GameFilterState.DisabledDefault;

        var mode = "disabled";
        string? tcpCandidate = null;
        string? udpCandidate = null;

        foreach (var raw in File.ReadLines(flag))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            var eq = line.IndexOf('=');
            var key = (eq >= 0 ? line[..eq] : line).Trim();
            var value = eq >= 0 ? line[(eq + 1)..].Trim() : "";

            if (key.Equals("mode", StringComparison.OrdinalIgnoreCase) && value.Length > 0)
                mode = value.ToLowerInvariant();
            else if (key.Equals("all", StringComparison.OrdinalIgnoreCase) && value.Length == 0)
                mode = "all";
            else if (key.Equals("udp", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Length == 0) mode = "udp";
                else udpCandidate = value;
            }
            else if (key.Equals("tcp", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Length == 0) mode = "tcp";
                else tcpCandidate = value;
            }
        }

        var tcp = GameFilterState.DefaultRange;
        var udp = GameFilterState.DefaultRange;
        if (tcpCandidate is not null && TryNormalizePortRange(tcpCandidate, out var parsedTcp))
            tcp = parsedTcp;
        if (udpCandidate is not null && TryNormalizePortRange(udpCandidate, out var parsedUdp))
            udp = parsedUdp;

        mode = NormalizeMode(mode);
        return new GameFilterState(mode, tcp, udp);
    }

    public static bool TryNormalizePortRange(string? value, out string normalized)
    {
        normalized = GameFilterState.DefaultRange;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var compact = CompactPortRange(value);
        if (compact.Length == 0)
            return false;

        foreach (var item in compact.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!IsValidPortItem(item))
                return false;
        }

        normalized = compact;
        return true;
    }

    public static string CompactPortRange(string? value) =>
        string.IsNullOrEmpty(value) ? "" : value.Replace(" ", "", StringComparison.Ordinal);

    public static string SanitizePortRangeInput(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var chars = value.Where(static c => c is >= '0' and <= '9' or '-' or ',').ToArray();
        return new string(chars);
    }

    public static bool IsPortRangeInputChar(string? text) =>
        !string.IsNullOrEmpty(text) && text.All(static c => c is >= '0' and <= '9' or '-' or ',');

    private void WriteGameFilter(GameFilterState state)
    {
        Directory.CreateDirectory(_paths.Utils);
        var flag = Path.Combine(_paths.Utils, "game_filter.enabled");
        var text =
            $"mode={NormalizeMode(state.Mode)}{Environment.NewLine}" +
            $"tcp={state.TcpRange}{Environment.NewLine}" +
            $"udp={state.UdpRange}{Environment.NewLine}";
        File.WriteAllText(flag, text);
    }

    private static string NormalizeMode(string? mode) =>
        mode?.Trim().ToLowerInvariant() switch
        {
            "all" => "all",
            "tcp" => "tcp",
            "udp" => "udp",
            _ => "disabled"
        };

    private static bool IsValidPortItem(string item)
    {
        if (!PortItemRegex.IsMatch(item))
            return false;

        var dash = item.IndexOf('-');
        var startText = dash >= 0 ? item[..dash] : item;
        var endText = dash >= 0 ? item[(dash + 1)..] : item;
        if (startText.Length > 5 || endText.Length > 5)
            return false;
        if (!int.TryParse(startText, out var start) || !int.TryParse(endText, out var end))
            return false;
        return start is >= 1 and <= 65535 && end is >= 1 and <= 65535 && start <= end;
    }

    public string GetIpsetStatus()
    {
        var listFile = Path.Combine(_paths.Lists, "ipset-all.txt");
        if (!File.Exists(listFile)) return "none";

        var lines = File.ReadAllLines(listFile).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        if (lines.Length == 0) return "any";
        if (lines.Any(l => l.Trim() == "203.0.113.113/32")) return "none";
        return "loaded";
    }

    public void SetIpsetFilter(string mode)
    {
        var listFile = Path.Combine(_paths.Lists, "ipset-all.txt");
        var backupFile = listFile + ".backup";
        Directory.CreateDirectory(_paths.Lists);

        var current = GetIpsetStatus();
        if (current == mode.ToLowerInvariant())
            return;

        switch (mode.ToLowerInvariant())
        {
            case "none":
                if (current == "loaded" && File.Exists(listFile))
                {
                    if (File.Exists(backupFile)) File.Delete(backupFile);
                    File.Move(listFile, backupFile);
                }
                File.WriteAllText(listFile, "203.0.113.113/32" + Environment.NewLine);
                break;
            case "any":
                if (current == "loaded" && File.Exists(listFile))
                {
                    if (File.Exists(backupFile)) File.Delete(backupFile);
                    File.Move(listFile, backupFile);
                }
                File.WriteAllText(listFile, "");
                break;
            case "loaded":
                if (File.Exists(backupFile))
                {
                    if (File.Exists(listFile)) File.Delete(listFile);
                    File.Move(backupFile, listFile);
                }
                else
                    throw new InvalidOperationException("No backup to restore. Update IPSet list first.");
                break;
            default:
                throw new ArgumentException($"Unknown IPSet mode: {mode}", nameof(mode));
        }
    }

    public bool IsAutoUpdateEnabled() =>
        File.Exists(Path.Combine(_paths.Utils, "check_updates.enabled"));

    public void SetAutoUpdate(bool enabled)
    {
        var flag = Path.Combine(_paths.Utils, "check_updates.enabled");
        Directory.CreateDirectory(_paths.Utils);
        if (enabled)
        {
            if (!File.Exists(flag))
                File.WriteAllText(flag, "ENABLED");
            return;
        }

        if (File.Exists(flag))
            File.Delete(flag);
    }
}
