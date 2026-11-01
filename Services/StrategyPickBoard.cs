using Aeroway.Helpers;
using Aeroway.Models;

namespace Aeroway.Services;

/// <summary>
/// Очередь кандидатов подбора: ожидание, текущая проверка, готовый вердикт.
/// </summary>
public sealed class StrategyPickBoard
{
    private readonly List<StrategyPickCandidate> _candidates = [];

    public IReadOnlyList<StrategyPickCandidate> Candidates => _candidates;
    public IReadOnlyList<TestTargetRow> Baseline { get; private set; } = [];
    public bool HasBaseline { get; private set; }
    public int BaselineOpen { get; private set; }
    public int BaselineClosed { get; private set; }
    public PickPhase Phase { get; internal set; } = PickPhase.Idle;
    public string StatusText { get; internal set; } = "";
    public double Progress { get; internal set; }
    public string ProgressText { get; internal set; } = "";
    public string? RunningFile { get; private set; }
    public string? ReviewFile { get; internal set; }

    public void Seed(IReadOnlyList<StrategyItem> items)
    {
        _candidates.Clear();
        foreach (var item in items)
        {
            _candidates.Add(new StrategyPickCandidate
            {
                FileName = Canon(item.FileName),
                DisplayName = item.DisplayName
            });
        }
    }

    public void SetBaseline(IReadOnlyList<TestTargetRow> rows)
    {
        Baseline = Clone(rows);
        var (open, closed) = StrategyPickVerdict.CountSites(Baseline);
        BaselineOpen = open;
        BaselineClosed = closed;
        HasBaseline = Baseline.Count > 0 && (open > 0 || closed > 0 || StrategyPickVerdict.HasMeasuredSite(Baseline));
    }

    public void SyncLive(
        string? currentFile,
        IReadOnlyList<TestTargetRow> liveTargets,
        IReadOnlyList<StrategyPickCompletion> completed)
    {
        foreach (var item in completed)
            MarkDone(item.FileName, item.Targets, item.ScoreGlyph, item.RankScore);

        if (string.IsNullOrWhiteSpace(currentFile))
            return;

        var candidate = FindOrAdd(currentFile);
        if (candidate.State == PickQueueState.Done)
        {
            RunningFile = candidate.FileName;
            return;
        }

        foreach (var other in _candidates)
        {
            if (ReferenceEquals(other, candidate) || other.State != PickQueueState.Running)
                continue;

            if (StrategyPickVerdict.HasMeasuredSite(other.Targets))
                MarkDone(other.FileName, other.Targets, scoreGlyph: null, rankScore: other.RankScore);
            else
                other.State = PickQueueState.Pending;
        }

        candidate.State = PickQueueState.Running;
        candidate.Targets = Clone(liveTargets);
        RunningFile = candidate.FileName;
    }

    public void Finish(bool stopped)
    {
        Phase = stopped ? PickPhase.Stopped : PickPhase.Finished;
        if (!stopped)
        {
            foreach (var candidate in _candidates)
            {
                if (candidate.State != PickQueueState.Done)
                    MarkSkipped(candidate);
            }

            return;
        }

        foreach (var candidate in _candidates)
        {
            if (candidate.State != PickQueueState.Running)
                continue;

            if (StrategyPickVerdict.HasMeasuredSite(candidate.Targets))
                MarkDone(candidate.FileName, candidate.Targets, scoreGlyph: null, rankScore: candidate.RankScore);
            else
                candidate.State = PickQueueState.Pending;
        }

        RunningFile = null;
    }

    public bool TrySelect(string fileName)
    {
        if (Phase is PickPhase.Baseline or PickPhase.Testing)
            return false;

        var candidate = Find(fileName);
        if (candidate is null)
            return false;

        ReviewFile = candidate.FileName;
        return true;
    }

    public StrategyPickCandidate? Find(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var canon = Canon(fileName);
        return _candidates.FirstOrDefault(candidate =>
            candidate.FileName.Equals(canon, StringComparison.OrdinalIgnoreCase));
    }

    public StrategyPickCandidate? Best()
    {
        var done = _candidates.Where(candidate =>
            candidate.State == PickQueueState.Done
            && candidate.Assessment is { Verdict: not PickVerdict.None }
            && candidate.Assessment.Reason != PickReason.Skipped).ToList();
        if (done.Count == 0)
            return null;

        return done
            .OrderByDescending(candidate => candidate.Assessment!.UnblockedCount)
            .ThenBy(candidate => candidate.Assessment!.StillClosedCount + candidate.Assessment!.RegressedCount)
            .ThenByDescending(candidate => VerdictWeight(candidate.Assessment!.Verdict))
            .ThenByDescending(candidate => candidate.RankScore)
            .ThenBy(candidate => candidate.FileName, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private void MarkDone(string fileName, IReadOnlyList<TestTargetRow> targets, string? scoreGlyph, int rankScore)
    {
        var candidate = FindOrAdd(fileName);
        var rows = Clone(targets);
        var assessment = StrategyPickVerdict.Assess(HasBaseline ? Baseline : null, rows, scoreGlyph);
        candidate.State = PickQueueState.Done;
        candidate.Targets = rows;
        candidate.Assessment = assessment;
        candidate.Glyph = string.IsNullOrEmpty(assessment.Glyph) ? scoreGlyph ?? "" : assessment.Glyph;
        candidate.RankScore = rankScore;
    }

    private static void MarkSkipped(StrategyPickCandidate candidate)
    {
        candidate.State = PickQueueState.Done;
        candidate.Assessment = StrategyPickVerdict.Skipped();
        candidate.Glyph = candidate.Assessment.Glyph;
        candidate.Targets = [];
    }

    private StrategyPickCandidate FindOrAdd(string fileName)
    {
        var existing = Find(fileName);
        if (existing is not null)
            return existing;

        var canon = Canon(fileName);
        var created = new StrategyPickCandidate
        {
            FileName = canon,
            DisplayName = StrategyDisplayHelper.ToDisplayName(canon)
        };
        _candidates.Add(created);
        return created;
    }

    private static int VerdictWeight(PickVerdict verdict) => verdict switch
    {
        PickVerdict.Works => 2,
        PickVerdict.Partial => 1,
        _ => 0
    };

    internal static string Canon(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        if (!name.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            name += ".bat";
        return name;
    }

    private static List<TestTargetRow> Clone(IEnumerable<TestTargetRow> rows) =>
        rows.Select(row => new TestTargetRow
        {
            Name = row.Name,
            PingOnly = row.PingOnly,
            Http = row.Http,
            Tls12 = row.Tls12,
            Tls13 = row.Tls13,
            Ping = row.Ping
        }).ToList();
}
