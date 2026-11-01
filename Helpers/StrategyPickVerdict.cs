using Aeroway.Models;

namespace Aeroway.Helpers;

/// <summary>
/// Сравнивает проверку без обхода с результатом стратегии.
/// «Работает» делится на разблокировку обходом и доступ, который и так был открыт.
/// </summary>
public static class StrategyPickVerdict
{
    public static PickAccess AccessOf(TestTargetRow row)
    {
        if (row.PingOnly)
            return PickAccess.Unknown;

        var anyKnown = false;
        var anyOpen = false;
        var anyClosed = false;
        foreach (var token in new[] { row.Http, row.Tls12, row.Tls13 })
        {
            if (IsPending(token))
                continue;

            anyKnown = true;
            if (IsOpen(token))
                anyOpen = true;
            else if (IsClosed(token))
                anyClosed = true;
        }

        if (!anyKnown)
            return PickAccess.Unknown;
        if (anyOpen)
            return PickAccess.Open;
        if (anyClosed)
            return PickAccess.Closed;
        return PickAccess.Unknown;
    }

    public static bool HasMeasuredSite(IReadOnlyList<TestTargetRow> rows) =>
        rows.Any(row => AccessOf(row) != PickAccess.Unknown);

    public static (int Open, int Closed) CountSites(IReadOnlyList<TestTargetRow> rows)
    {
        var open = 0;
        var closed = 0;
        foreach (var row in rows)
        {
            switch (AccessOf(row))
            {
                case PickAccess.Open:
                    open++;
                    break;
                case PickAccess.Closed:
                    closed++;
                    break;
            }
        }

        return (open, closed);
    }

    public static StrategyPickAssessment Assess(
        IReadOnlyList<TestTargetRow>? baseline,
        IReadOnlyList<TestTargetRow> strategy,
        string? scoreGlyph)
    {
        var baselineMap = Index(baseline);
        var hasBaseline = baseline is { Count: > 0 };
        var deltas = new Dictionary<string, TargetDeltaKind>(StringComparer.OrdinalIgnoreCase);
        var unblocked = 0;
        var already = 0;
        var still = 0;
        var regressed = 0;
        var comparable = 0;

        foreach (var row in strategy)
        {
            if (row.PingOnly)
                continue;

            var key = StrategyPickAssessment.Normalize(row.Name);
            var with = AccessOf(row);
            var before = PickAccess.Unknown;
            if (baselineMap.TryGetValue(key, out var baseRow))
                before = AccessOf(baseRow);

            var kind = Classify(before, with, hasBaseline);
            deltas[key] = kind;
            switch (kind)
            {
                case TargetDeltaKind.Unblocked:
                    unblocked++;
                    comparable++;
                    break;
                case TargetDeltaKind.AlreadyOpen:
                    already++;
                    comparable++;
                    break;
                case TargetDeltaKind.StillClosed:
                    still++;
                    comparable++;
                    break;
                case TargetDeltaKind.Regressed:
                    regressed++;
                    comparable++;
                    break;
            }
        }

        if (!hasBaseline || comparable == 0)
            return FromGlyph(scoreGlyph, deltas);

        if (unblocked > 0 && still == 0 && regressed == 0)
        {
            return Build(PickVerdict.Works, PickReason.UnblockedByBypass, true,
                unblocked, already, still, regressed, deltas);
        }

        if (unblocked == 0 && still == 0 && regressed == 0 && already > 0)
        {
            return Build(PickVerdict.Works, PickReason.AlreadyOpen, true,
                unblocked, already, still, regressed, deltas);
        }

        if (unblocked > 0)
        {
            return Build(PickVerdict.Partial, PickReason.Mixed, true,
                unblocked, already, still, regressed, deltas);
        }

        return Build(PickVerdict.Failed, PickReason.NoImprovement, true,
            unblocked, already, still, regressed, deltas);
    }

    public static StrategyPickAssessment Skipped() =>
        Build(PickVerdict.Failed, PickReason.Skipped, false, 0, 0, 0, 0,
            new Dictionary<string, TargetDeltaKind>(StringComparer.OrdinalIgnoreCase));

    private static StrategyPickAssessment FromGlyph(
        string? scoreGlyph,
        Dictionary<string, TargetDeltaKind> deltas)
    {
        var (verdict, reason) = scoreGlyph switch
        {
            "✓" => (PickVerdict.Works, PickReason.ScoreOnly),
            "≈" => (PickVerdict.Partial, PickReason.ScoreOnly),
            "✗" => (PickVerdict.Failed, PickReason.ScoreOnly),
            _ => (PickVerdict.None, PickReason.None)
        };
        return Build(verdict, reason, false, 0, 0, 0, 0, deltas);
    }

    private static TargetDeltaKind Classify(PickAccess before, PickAccess with, bool hasBaseline)
    {
        if (!hasBaseline || before == PickAccess.Unknown || with == PickAccess.Unknown)
            return TargetDeltaKind.Unknown;
        if (before != PickAccess.Open && with == PickAccess.Open)
            return TargetDeltaKind.Unblocked;
        if (before == PickAccess.Open && with == PickAccess.Open)
            return TargetDeltaKind.AlreadyOpen;
        if (before == PickAccess.Open && with == PickAccess.Closed)
            return TargetDeltaKind.Regressed;
        if (with == PickAccess.Closed)
            return TargetDeltaKind.StillClosed;
        return TargetDeltaKind.Unknown;
    }

    private static Dictionary<string, TestTargetRow> Index(IReadOnlyList<TestTargetRow>? rows)
    {
        var map = new Dictionary<string, TestTargetRow>(StringComparer.OrdinalIgnoreCase);
        if (rows is null)
            return map;

        foreach (var row in rows)
        {
            if (row.PingOnly)
                continue;
            map[StrategyPickAssessment.Normalize(row.Name)] = row;
        }

        return map;
    }

    private static StrategyPickAssessment Build(
        PickVerdict verdict,
        PickReason reason,
        bool usedBaseline,
        int unblocked,
        int already,
        int still,
        int regressed,
        Dictionary<string, TargetDeltaKind> deltas) =>
        new()
        {
            Verdict = verdict,
            Reason = reason,
            UsedBaseline = usedBaseline,
            UnblockedCount = unblocked,
            AlreadyOpenCount = already,
            StillClosedCount = still,
            RegressedCount = regressed,
            Deltas = deltas
        };

    private static bool IsPending(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return true;

        var text = token.Trim();
        return text is "…" or "-" or "n/a" || text.EndsWith(":…", StringComparison.Ordinal);
    }

    private static bool IsOpen(string token)
    {
        var text = token.ToUpperInvariant();
        if (text.Contains("UNSUP", StringComparison.Ordinal) || text.Contains("НЕПОДД", StringComparison.Ordinal))
            return false;
        return text.Contains("OK", StringComparison.Ordinal) || text.Contains("ОК", StringComparison.Ordinal);
    }

    private static bool IsClosed(string token)
    {
        var text = token.ToUpperInvariant();
        return text.Contains("ERR", StringComparison.Ordinal)
               || text.Contains("FAIL", StringComparison.Ordinal)
               || text.Contains("SSL", StringComparison.Ordinal)
               || text.Contains("ОШ", StringComparison.Ordinal)
               || text.Contains("BLOCK", StringComparison.Ordinal);
    }
}
