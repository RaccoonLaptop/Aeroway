using System.Text.RegularExpressions;
using ZapretUI.Models;

namespace ZapretUI.Helpers;

public static class TestRunParser
{
    private static readonly Regex ConfigHeader = new(
        @"\[\s*(?<cur>\d+)\s*/\s*(?<tot>\d+)\s*\]\s*(?<name>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BestConfig = new(
        @"^Best config:\s*(?<name>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DpiTargetHeader = new(
        @"^===\s*\[(?<country>[^\]]*)\]\[(?<provider>[^\]]*)\]\s*(?<id>.+?)\s*===$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DpiStatusLine = new(
        @"\[(?<label>HTTP|TLS\s*1\.2|TLS\s*1\.3)\].*status=(?<status>[A-Za-z_]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool TryParseConfigHeader(string line, out int current, out int total, out string fileName)
    {
        current = 0;
        total = 0;
        fileName = "";
        var m = ConfigHeader.Match(line.Trim());
        if (!m.Success) return false;
        current = int.Parse(m.Groups["cur"].Value);
        total = int.Parse(m.Groups["tot"].Value);
        fileName = m.Groups["name"].Value.Trim();
        return true;
    }

    public static bool TryParseBestConfig(string line, out string fileName)
    {
        fileName = "";
        var m = BestConfig.Match(line.Trim());
        if (!m.Success) return false;
        fileName = m.Groups["name"].Value.Trim();
        return true;
    }

    public static bool TryParseTargetRow(string line, out TestTargetRow row)
    {
        row = null!;
        if (!TestTableLineFormatter.TryParseTestRow(line, out var parsed))
            return false;

        row = new TestTargetRow
        {
            Name = parsed.Name,
            Http = parsed.Http ?? "…",
            Tls12 = parsed.Tls12 ?? "…",
            Tls13 = parsed.Tls13 ?? "…",
            Ping = parsed.Ping,
            PingOnly = parsed.PingOnly
        };
        return true;
    }

    public static bool TryParseDpiTargetHeader(string line, out string targetName)
    {
        targetName = "";
        var m = DpiTargetHeader.Match(line.Trim());
        if (!m.Success) return false;

        var country = m.Groups["country"].Value.Trim();
        var id = m.Groups["id"].Value.Trim();
        var provider = m.Groups["provider"].Value.Trim();

        // Id distinguishes several hosts of the same provider (US.CF-01 and US.CF-02).
        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(provider))
            targetName = string.IsNullOrEmpty(country) ? $"{id} ({provider})" : $"[{country}] {provider} {id}";
        else if (!string.IsNullOrEmpty(provider))
            targetName = string.IsNullOrEmpty(country) ? provider : $"[{country}] {provider}";
        else if (!string.IsNullOrEmpty(country))
            targetName = string.IsNullOrEmpty(id) ? $"[{country}]" : $"[{country}] {id}";
        else
            targetName = id;

        return true;
    }

    public static bool TryParseDpiStatusLine(string line, out string label, out string status)
    {
        label = "";
        status = "";
        var m = DpiStatusLine.Match(line);
        if (!m.Success) return false;
        label = m.Groups["label"].Value;
        status = m.Groups["status"].Value;
        return true;
    }

    public static bool TryParseScoreRow(string line, out PresetScoreRow row)
    {
        row = null!;
        if (TryParseStandardScore(line, out row))
            return true;
        return TryParseDpiScore(line, out row);
    }

    private static bool TryParseStandardScore(string line, out PresetScoreRow row)
    {
        row = null!;
        if (!TestTableLineFormatter.TryParseAnalyticsRow(line, out var parsed))
            return false;

        var httpOk = ExtractInt(parsed.HttpOk);
        var err = ExtractInt(parsed.Err);
        var unsup = ExtractInt(parsed.Unsup);
        var pingOk = ExtractInt(parsed.PingOk);
        var fail = ExtractInt(parsed.Fail);
        var fileName = NormalizeBat(parsed.Config.Trim());

        row = BuildScore(fileName, httpOk, fail, err, pingOk, unsup,
            $"HTTP OK: {httpOk}, ERR: {err}, FAIL: {fail}, Ping OK: {pingOk}, UNSUP: {unsup}");
        return true;
    }

    private static bool TryParseDpiScore(string line, out PresetScoreRow row)
    {
        row = null!;
        var m = Regex.Match(line.Trim(),
            @"^(?<config>.+?)\s*:\s*OK:\s*(?<ok>\d+)\s*,\s*FAIL:\s*(?<fail>\d+)\s*,\s*UNSUP:\s*(?<unsup>\d+)\s*,\s*BLOCKED:\s*(?<blocked>\d+)\s*$",
            RegexOptions.CultureInvariant);
        if (!m.Success) return false;

        var fileName = NormalizeBat(m.Groups["config"].Value.Trim());
        var ok = int.Parse(m.Groups["ok"].Value);
        var fail = int.Parse(m.Groups["fail"].Value);
        var unsup = int.Parse(m.Groups["unsup"].Value);
        var blocked = int.Parse(m.Groups["blocked"].Value);
        var totalFail = fail + blocked;

        row = BuildScore(fileName, ok, totalFail, fail, 0, unsup,
            $"OK: {ok}, FAIL: {fail}, BLOCKED: {blocked}, UNSUP: {unsup}");
        return true;
    }

    private static PresetScoreRow BuildScore(string fileName, int httpOk, int fail, int err, int pingOk, int unsup, string detail)
    {
        var glyph = fail == 0 && err == 0 ? "✓" : fail <= 1 ? "≈" : "✗";
        var rank = httpOk * 10 - fail * 5 - err * 3 + pingOk;
        return new PresetScoreRow
        {
            FileName = fileName,
            DisplayName = StrategyDisplayHelper.ToDisplayName(fileName),
            Detail = detail,
            Glyph = glyph,
            HttpOk = httpOk,
            Fail = fail,
            RankScore = rank
        };
    }

    private static string NormalizeBat(string fileName)
    {
        if (!fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            fileName += ".bat";
        return fileName;
    }

    private static int ExtractInt(string labeled)
    {
        var m = Regex.Match(labeled, @"(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }
}
