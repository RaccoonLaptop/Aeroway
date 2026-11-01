namespace Aeroway.Helpers;

internal static class BridgeOutputFormatter
{
    public static string ForDialog(string action, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        if (action is "InstallService" or "RemoveServices")
            return SummarizeServiceAction(action, raw);

        if (action == "RunDiagnostics")
            raw = DiagnosticOutputLocalizer.Localize(raw);

        return FilterVerboseLines(raw);
    }

    private static string SummarizeServiceAction(string action, string raw)
    {
        var lines = SplitLines(raw);
        var errors = lines.Where(IsErrorLine).ToList();
        if (errors.Count > 0)
            return string.Join(Environment.NewLine, errors);

        if (action == "InstallService")
        {
            var strategy = ExtractStrategyName(lines);
            return string.IsNullOrEmpty(strategy)
                ? Loc.T("service.install_ok_short")
                : Loc.F("service.install_ok", strategy);
        }

        return Loc.T("service.remove_ok");
    }

    private static string FilterVerboseLines(string raw) =>
        string.Join(Environment.NewLine,
            SplitLines(raw).Where(l => !ShouldHideLine(l)));

    private static IEnumerable<string> SplitLines(string raw) =>
        raw.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(ColoredLog.StripColorTag)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);

    private static bool ShouldHideLine(string line)
    {
        if (line.Length > 240)
            return true;

        var lower = line.ToLowerInvariant();
        return lower.StartsWith("args:")
               || lower.StartsWith("installing service with strategy:");
    }

    private static bool IsErrorLine(string line)
    {
        var lower = line.ToLowerInvariant();
        return lower.Contains("failed")
               || lower.Contains("not found")
               || lower.Contains("could not")
               || lower.Contains("error")
               || lower.Contains("ошибка");
    }

    private static string? ExtractStrategyName(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            const string prefix = "Installing service with strategy:";
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return line[prefix.Length..].Trim();

            const string okPrefix = "Service installed:";
            if (line.StartsWith(okPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var file = line[okPrefix.Length..].Trim();
                return Path.GetFileNameWithoutExtension(file);
            }
        }

        return null;
    }
}
