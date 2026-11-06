using System.Text.RegularExpressions;
using Aeroway.Services;

namespace Aeroway.Helpers;

/// <summary>
/// Translates ui-bridge RunDiagnostics output when UI language is Russian.
/// </summary>
internal static class DiagnosticOutputLocalizer
{
    private static readonly Dictionary<string, string> ExactLines = new(StringComparer.Ordinal)
    {
        ["[OK] Base Filtering Engine check passed"] = "diag.bfe_ok",
        ["[X] Base Filtering Engine is not running. This service is required for zapret to work"] = "diag.bfe_fail",
        ["[?] Make sure it's valid or disable it if you don't use a proxy"] = "diag.proxy_hint",
        ["[OK] Proxy check passed"] = "diag.proxy_ok",
        ["[OK] TCP timestamps check passed"] = "diag.tcp_ok",
        ["[OK] TCP timestamps successfully enabled"] = "diag.tcp_enabled",
        ["[X] Failed to enable TCP timestamps"] = "diag.tcp_fail",
        ["[X] Adguard process found. Adguard may cause problems with Discord"] = "diag.adguard_fail",
        ["[X] https://github.com/Flowseal/zapret-discord-youtube/issues/417"] = "diag.adguard_link",
        ["[OK] Adguard check passed"] = "diag.adguard_ok",
        ["[X] Killer services found. Killer conflicts with zapret"] = "diag.killer_fail",
        ["[X] https://github.com/Flowseal/zapret-discord-youtube/issues/2512#issuecomment-2821119513"] = "diag.killer_link",
        ["[OK] Killer check passed"] = "diag.killer_ok",
        ["[X] Intel Connectivity Network Service found. It conflicts with zapret"] = "diag.intel_fail",
        ["[X] https://github.com/ValdikSS/GoodbyeDPI/issues/541#issuecomment-2661670982"] = "diag.intel_link",
        ["[OK] Intel Connectivity check passed"] = "diag.intel_ok",
        ["[X] Check Point services found. Check Point conflicts with zapret"] = "diag.checkpoint_fail",
        ["[X] Try to uninstall Check Point"] = "diag.checkpoint_hint",
        ["[OK] Check Point check passed"] = "diag.checkpoint_ok",
        ["[X] SmartByte services found. SmartByte conflicts with zapret"] = "diag.smartbyte_fail",
        ["[X] Try to uninstall or disable SmartByte through services.msc"] = "diag.smartbyte_hint",
        ["[OK] SmartByte check passed"] = "diag.smartbyte_ok",
        ["[?] The path where Zapret is installed contains Cyrillic characters"] = "diag.cyrillic_warn",
        ["[?] If bypass doesn't work, try to move Zapret to another directory, for example in C:\\zapret"] = "diag.move_hint",
        ["[OK] Cyrillic path check passed"] = "diag.cyrillic_ok",
        ["[X] Zapret is installed in a OneDrive folder"] = "diag.onedrive_fail",
        ["[X] If bypass doesn't work, try to move Zapret to another directory, for example in C:\\zapret"] = "diag.move_hint_error",
        ["[OK] OneDrive check passed"] = "diag.onedrive_ok",
        ["[X] WinDivert64.sys file NOT found."] = "diag.windivert_missing",
        ["[OK] WinDivert driver present"] = "diag.windivert_ok",
        ["[OK] WinDivert hide driver present (Monkey64.sys)"] = "diag.windivert_hide_ok",
        ["[?] winws.exe is not running but Monkey service is active. Attempting to delete Monkey..."] = "diag.monkey_orphan",
        ["[X] Failed to delete Monkey. Check manually if any other bypass is using it."] = "diag.monkey_delete_fail",
        ["[OK] Monkey successfully removed"] = "diag.monkey_removed",
        ["[?] Make sure that all VPNs are disabled"] = "diag.vpn_hint",
        ["[OK] VPN check passed"] = "diag.vpn_ok",
        ["[OK] Secure DNS check passed"] = "diag.dns_ok",
        ["[?] Make sure you have configured secure DNS in a browser with some non-default DNS service provider,"] = "diag.dns_hint1",
        ["[?] If you use Windows 11 you can configure encrypted DNS in the Settings to hide this warning"] = "diag.dns_hint2",
        ["[?] Your hosts file contains entries for youtube.com or youtu.be. This may cause problems with YouTube access"] = "diag.hosts_youtube",
        ["[?] winws.exe is not running but WinDivert service is active. Attempting to delete WinDivert..."] = "diag.windivert_orphan",
        ["[X] Failed to delete WinDivert. Checking for conflicting services..."] = "diag.windivert_delete_fail",
        ["[?] Found conflicting service: GoodbyeDPI. Stopping and removing..."] = "diag.goodbyedpi_remove",
        ["[OK] WinDivert successfully deleted after removing conflicting services"] = "diag.windivert_deleted_after_conflict",
        ["[X] WinDivert still cannot be deleted. Check manually if any other bypass is using WinDivert."] = "diag.windivert_still_blocked",
        ["[X] No conflicting services found. Check manually if any other bypass is using WinDivert."] = "diag.windivert_manual",
        ["[OK] WinDivert successfully removed"] = "diag.windivert_removed",
        ["[OK] No conflicting bypass services"] = "diag.conflicts_ok",
        ["[OK] Bypass (winws.exe) is RUNNING"] = "diag.winws_running",
        ["[?] Bypass (winws.exe) is NOT running"] = "diag.winws_stopped",
        ["Diagnostics complete"] = "diag.complete",
    };

    private static readonly (Regex Pattern, string Key)[] PatternLines =
    [
        (new(@"^\[\?\] System proxy is enabled: (.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant), "diag.proxy_enabled"),
        (new(@"^\[\?\] VPN services found: (.+)\. Some VPNs can conflict with zapret$", RegexOptions.Compiled | RegexOptions.CultureInvariant), "diag.vpn_found"),
        (new(@"^\[X\] Conflicting bypass services found: (.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant), "diag.conflicts_found"),
        (new(@"^  Stopping and removing service: (.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant), "diag.service_stopping"),
        (new(@"^\[OK\] Successfully removed service: (.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant), "diag.removed_ok"),
        (new(@"^\[X\] Failed to remove service: (.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant), "diag.removed_fail"),
        (new(@"^\[OK\] Zapret is installed in: (.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant), "diag.install_path"),
    ];

    public static string Localize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || LocalizationService.IsEnglish)
            return raw?.TrimEnd() ?? "";

        var lines = raw.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => TranslateLine(ColoredLog.StripColorTag(line).Trim()))
            .Where(line => line.Length > 0);

        return string.Join(Environment.NewLine, lines);
    }

    private static string TranslateLine(string line)
    {
        if (ExactLines.TryGetValue(line, out var key))
            return Loc.T(key);

        foreach (var (pattern, patternKey) in PatternLines)
        {
            var match = pattern.Match(line);
            if (!match.Success) continue;
            return Loc.F(patternKey, match.Groups[1].Value);
        }

        return line;
    }
}
