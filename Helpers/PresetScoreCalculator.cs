using ZapretUI.Models;

namespace ZapretUI.Helpers;

public static class PresetScoreCalculator
{
    public static PresetScoreRow FromStandardTargets(string fileName, IReadOnlyList<TestTargetRow> targets)
    {
        var httpOk = 0;
        var err = 0;
        var unsup = 0;
        var pingOk = 0;
        var pingFail = 0;

        foreach (var row in targets)
        {
            if (!row.PingOnly)
            {
                CountToken(row.Http, ref httpOk, ref err, ref unsup);
                CountToken(row.Tls12, ref httpOk, ref err, ref unsup);
                CountToken(row.Tls13, ref httpOk, ref err, ref unsup);
            }

            if (string.IsNullOrWhiteSpace(row.Ping) || row.Ping is "…")
                continue;

            if (IsPingFail(row.Ping))
                pingFail++;
            else
                pingOk++;
        }

        return BuildScore(
            fileName,
            httpOk,
            pingFail,
            err,
            pingOk,
            unsup,
            $"HTTP OK: {httpOk}, ERR: {err}, FAIL: {pingFail}, Ping OK: {pingOk}, UNSUP: {unsup}");
    }

    public static PresetScoreRow FromDpiTargets(string fileName, IReadOnlyList<TestTargetRow> targets)
    {
        var ok = 0;
        var fail = 0;
        var unsup = 0;
        var blocked = 0;

        foreach (var row in targets)
        {
            CountDpiStatus(row.Http, ref ok, ref fail, ref unsup, ref blocked);
            CountDpiStatus(row.Tls12, ref ok, ref fail, ref unsup, ref blocked);
            CountDpiStatus(row.Tls13, ref ok, ref fail, ref unsup, ref blocked);
        }

        var totalFail = fail + blocked;
        var summary = $"OK: {ok}, FAIL: {fail}, BLOCKED: {blocked}, UNSUP: {unsup}";
        var detail = targets.Count <= 1
            ? summary
            : summary + Environment.NewLine + string.Join(Environment.NewLine, targets.Select(DescribeDpiRow));
        return BuildScore(
            fileName,
            ok,
            totalFail,
            fail,
            0,
            unsup,
            detail);
    }

    private static string DescribeDpiRow(TestTargetRow row)
    {
        static string Part(string token) =>
            string.IsNullOrWhiteSpace(token) || token is "…" ? "…" : token;

        return $"{row.Name}: {Part(row.Http)} | {Part(row.Tls12)} | {Part(row.Tls13)}";
    }

    private static PresetScoreRow BuildScore(
        string fileName,
        int httpOk,
        int fail,
        int err,
        int pingOk,
        int unsup,
        string detail)
    {
        var glyph = fail == 0 && err == 0 ? "✓" : fail <= 1 ? "≈" : "✗";
        var rank = httpOk * 10 - fail * 5 - err * 3 + pingOk;
        return new PresetScoreRow
        {
            FileName = NormalizeBat(fileName),
            DisplayName = StrategyDisplayHelper.ToDisplayName(fileName),
            Detail = detail,
            Glyph = glyph,
            HttpOk = httpOk,
            Fail = fail,
            RankScore = rank
        };
    }

    private static void CountToken(string token, ref int ok, ref int err, ref int unsup)
    {
        if (string.IsNullOrWhiteSpace(token) || token is "…" or "HTTP:…" or "TLS1.2:…" or "TLS1.3:…")
            return;

        var t = token.ToUpperInvariant();
        if (t.Contains("UNSUP", StringComparison.Ordinal) || t.Contains("НЕПОДД", StringComparison.Ordinal))
            unsup++;
        else if (t.Contains("SSL", StringComparison.Ordinal)
                 || t.Contains("ERROR", StringComparison.Ordinal)
                 || t.Contains("ERR", StringComparison.Ordinal)
                 || t.Contains("FAIL", StringComparison.Ordinal)
                 || t.Contains("ОШ", StringComparison.Ordinal))
            err++;
        else if (t.Contains("OK", StringComparison.Ordinal) || t.Contains("ОК", StringComparison.Ordinal))
            ok++;
    }

    private static void CountDpiStatus(string status, ref int ok, ref int fail, ref int unsup, ref int blocked)
    {
        if (string.IsNullOrWhiteSpace(status) || status is "…")
            return;

        var t = status.ToUpperInvariant();
        if (t.Contains("LIKELY_BLOCKED", StringComparison.Ordinal) || t.Contains("BLOCKED", StringComparison.Ordinal))
            blocked++;
        else if (t.Contains("UNSUPPORTED", StringComparison.Ordinal) || t.Contains("UNSUP", StringComparison.Ordinal))
            unsup++;
        else if (t.Contains("FAIL", StringComparison.Ordinal))
            fail++;
        else if (t.Contains("OK", StringComparison.Ordinal))
            ok++;
    }

    private static bool IsPingFail(string ping) =>
        ping.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
        || ping.Contains("Таймаут", StringComparison.OrdinalIgnoreCase)
        || ping.Equals("n/a", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeBat(string fileName)
    {
        if (!fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            fileName += ".bat";
        return fileName;
    }
}
