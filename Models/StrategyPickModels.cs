namespace Aeroway.Models;

public enum PickQueueState
{
    Pending,
    Running,
    Done
}

public enum PickPhase
{
    Idle,
    Baseline,
    Testing,
    Finished,
    Stopped
}

public enum PickVerdict
{
    None,
    Works,
    Partial,
    Failed
}

public enum PickReason
{
    None,
    UnblockedByBypass,
    AlreadyOpen,
    Mixed,
    NoImprovement,
    ScoreOnly,
    Skipped
}

public enum PickAccess
{
    Unknown,
    Open,
    Closed
}

public enum TargetDeltaKind
{
    Unknown,
    Unblocked,
    AlreadyOpen,
    StillClosed,
    Regressed
}

public sealed class StrategyPickAssessment
{
    public PickVerdict Verdict { get; init; }
    public PickReason Reason { get; init; }
    public bool UsedBaseline { get; init; }
    public int UnblockedCount { get; init; }
    public int AlreadyOpenCount { get; init; }
    public int StillClosedCount { get; init; }
    public int RegressedCount { get; init; }
    public IReadOnlyDictionary<string, TargetDeltaKind> Deltas { get; init; }
        = new Dictionary<string, TargetDeltaKind>(StringComparer.OrdinalIgnoreCase);

    public string Glyph => Verdict switch
    {
        PickVerdict.Works => "✓",
        PickVerdict.Partial => "≈",
        PickVerdict.Failed => "✗",
        _ => ""
    };

    public TargetDeltaKind DeltaFor(string name)
    {
        var key = StrategyPickAssessment.Normalize(name);
        return Deltas.TryGetValue(key, out var kind) ? kind : TargetDeltaKind.Unknown;
    }

    public static string Normalize(string name) =>
        name.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
}

public sealed class StrategyPickCandidate
{
    public required string FileName { get; init; }
    public required string DisplayName { get; init; }
    public PickQueueState State { get; set; } = PickQueueState.Pending;
    public string Glyph { get; set; } = "";
    public int RankScore { get; set; }
    public StrategyPickAssessment? Assessment { get; set; }
    public IReadOnlyList<TestTargetRow> Targets { get; set; } = [];
}

public sealed class StrategyPickCompletion
{
    public required string FileName { get; init; }
    public required IReadOnlyList<TestTargetRow> Targets { get; init; }
    public string? ScoreGlyph { get; init; }
    public int RankScore { get; init; }
}
