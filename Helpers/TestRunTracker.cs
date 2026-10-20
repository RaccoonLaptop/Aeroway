using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using ZapretUI.Models;

namespace ZapretUI.Helpers;

public sealed class TestRunTracker
{
    private readonly StringBuilder _lineBuffer = new();
    private readonly Dictionary<string, TestTargetRow> _targetIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PresetTargetSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _finalizedPresets = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<TestTargetDefinition> _targetTemplate = [];
    private Dictionary<string, string> _templateNames = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentDpiTarget;

    public ObservableCollection<TestTargetRow> Targets { get; } = [];
    public ObservableCollection<PresetScoreRow> Scores { get; } = [];

    public PresetTestKind TestKind { get; private set; } = PresetTestKind.Standard;
    public string CurrentPreset { get; private set; } = "";
    public string CurrentPresetDisplay { get; private set; } = "";
    public string StatusText { get; private set; } = Loc.T("tools.test_status_idle");
    public string BestPresetFile { get; private set; } = "";
    public double Progress { get; private set; }
    public string ProgressText { get; private set; } = "";
    public bool IsRunning { get; private set; }

    public event Action? Changed;

    public bool TryGetSnapshot(string fileName, out PresetTargetSnapshot snapshot) =>
        _snapshots.TryGetValue(NormalizeBat(fileName), out snapshot!);

    public void BeginRun(PresetTestKind kind, IReadOnlyList<TestTargetDefinition>? targetTemplate = null)
    {
        TestKind = kind;
        Targets.Clear();
        Scores.Clear();
        _targetIndex.Clear();
        _snapshots.Clear();
        _finalizedPresets.Clear();
        _lineBuffer.Clear();
        _currentDpiTarget = null;
        _targetTemplate = targetTemplate ?? [];
        _templateNames = BuildTemplateNameMap(_targetTemplate);
        CurrentPreset = "";
        CurrentPresetDisplay = "";
        BestPresetFile = "";
        Progress = 0;
        ProgressText = "";
        IsRunning = true;
        StatusText = kind == PresetTestKind.DpiFreeze
            ? Loc.T("tools.test_status_running_dpi")
            : Loc.T("tools.test_status_running");

        if (kind == PresetTestKind.Standard && _targetTemplate.Count > 0)
            SeedStandardTargets();

        Notify();
    }

    public void EndRun(int exitCode)
    {
        FlushBuffer();
        FinalizeCurrentPresetIfNeeded();
        IsRunning = false;
        StatusText = exitCode == 0
            ? Loc.T("tools.test_status_done")
            : Loc.F("tools.test_exited", exitCode);
        Notify();
    }

    public void StopRun()
    {
        FlushBuffer();
        FinalizeCurrentPresetIfNeeded();
        IsRunning = false;
        StatusText = Loc.T("tools.test_stopped");
        Notify();
    }

    public void Feed(string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;

        var plain = StripAnsi(chunk)
            .Replace("\r\n", "\n")
            .Replace("\r", "");
        _lineBuffer.Append(plain);

        while (true)
        {
            var text = _lineBuffer.ToString();
            var nl = text.IndexOf('\n');
            if (nl < 0)
                break;

            var line = NormalizePhysicalLine(text[..nl]);
            _lineBuffer.Clear();
            _lineBuffer.Append(text[(nl + 1)..]);
            ProcessLine(line);
        }
    }

    private void ProcessLine(string line)
    {
        var plain = line.Trim();
        if (plain.Length == 0) return;

        if (TestRunParser.TryParseConfigHeader(plain, out var cur, out var tot, out var fileName))
        {
            var nextPreset = NormalizeBat(fileName);
            if (nextPreset.Equals(CurrentPreset, StringComparison.OrdinalIgnoreCase))
                return;

            FinalizeCurrentPresetIfNeeded();
            CurrentPreset = nextPreset;
            CurrentPresetDisplay = StrategyDisplayHelper.ToDisplayName(CurrentPreset);
            _currentDpiTarget = null;
            if (TestKind == PresetTestKind.Standard && _targetTemplate.Count > 0)
                SeedStandardTargets();
            else
            {
                Targets.Clear();
                _targetIndex.Clear();
            }
            if (tot > 0)
            {
                Progress = (double)cur / tot;
                ProgressText = $"{cur} / {tot}";
            }
            StatusText = Loc.F("tools.test_status_preset", CurrentPresetDisplay, cur, tot);
            Notify();
            return;
        }

        if (TestKind == PresetTestKind.Standard && TestRunParser.TryParseTargetRow(plain, out var target))
        {
            UpsertTarget(target);
            TryFinalizeIfTargetsComplete();
            return;
        }

        if (TestKind == PresetTestKind.DpiFreeze)
        {
            if (TestRunParser.TryParseDpiTargetHeader(plain, out var dpiName))
            {
                _currentDpiTarget = dpiName;
                UpsertTarget(new TestTargetRow
                {
                    Name = dpiName,
                    Http = "…",
                    Tls12 = "…",
                    Tls13 = "…",
                    Ping = "n/a",
                    PingOnly = false
                });
                return;
            }

            if (_currentDpiTarget is not null
                && TestRunParser.TryParseDpiStatusLine(plain, out var label, out var status)
                && _targetIndex.TryGetValue(NormalizeTargetKey(_currentDpiTarget), out var dpiRow))
            {
                var token = FormatDpiStatusToken(label, status);
                if (label.Equals("HTTP", StringComparison.OrdinalIgnoreCase))
                    dpiRow.Http = token;
                else if (label.Replace(" ", "", StringComparison.Ordinal).Contains("1.2", StringComparison.Ordinal))
                    dpiRow.Tls12 = token;
                else if (label.Replace(" ", "", StringComparison.Ordinal).Contains("1.3", StringComparison.Ordinal))
                    dpiRow.Tls13 = token;
                Notify();
                UpdateLiveDpiScore();
                return;
            }
        }

        if (TestRunParser.TryParseScoreRow(plain, out var score))
        {
            if (TestKind == PresetTestKind.DpiFreeze && HasTargetBreakdown(score.FileName))
                return;

            UpsertScore(score);
            return;
        }

        if (TestRunParser.TryParseBestConfig(plain, out var best))
        {
            BestPresetFile = NormalizeBat(best.Trim());
            StatusText = Loc.F("tools.test_best_found", StrategyDisplayHelper.ToDisplayName(BestPresetFile));
            Notify();
        }
    }

    private static string FormatDpiStatusToken(string label, string status)
    {
        var protocol = label.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant() switch
        {
            "HTTP" => "HTTP",
            "TLS1.2" => "TLS1.2",
            "TLS1.3" => "TLS1.3",
            _ => label.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant()
        };

        var normalized = status.ToUpperInvariant() switch
        {
            "UNSUPPORTED" => "UNSUP",
            "LIKELY_BLOCKED" => "BLOCKED",
            _ => status.ToUpperInvariant()
        };

        return $"{protocol}:{normalized}";
    }

    private void UpsertTarget(TestTargetRow target)
    {
        var name = TestKind == PresetTestKind.Standard && _targetTemplate.Count > 0
            ? ResolveCanonicalName(target.Name)
            : target.Name.Trim();
        var key = NormalizeTargetKey(name);

        if (TestKind == PresetTestKind.Standard && _targetTemplate.Count > 0)
        {
            if (!_targetIndex.TryGetValue(key, out var seeded))
                return;

            MergeTargetValues(seeded, target);
            Notify();
            return;
        }

        if (_targetIndex.TryGetValue(key, out var existing))
        {
            MergeTargetValues(existing, target);
        }
        else
        {
            var row = new TestTargetRow
            {
                Name = name,
                PingOnly = target.PingOnly,
                Http = target.Http,
                Tls12 = target.Tls12,
                Tls13 = target.Tls13,
                Ping = target.Ping
            };
            Targets.Add(row);
            _targetIndex[key] = row;
        }
        Notify();
    }

    private static void MergeTargetValues(TestTargetRow existing, TestTargetRow target)
    {
        if (!target.PingOnly)
        {
            if (target.Http is not ("…" or "HTTP:…"))
                existing.Http = target.Http;
            if (target.Tls12 is not ("…" or "TLS1.2:…"))
                existing.Tls12 = target.Tls12;
            if (target.Tls13 is not ("…" or "TLS1.3:…"))
                existing.Tls13 = target.Tls13;
        }
        if (target.Ping is not "…")
            existing.Ping = target.Ping;
    }

    private void UpdateLiveDpiScore()
    {
        if (TestKind != PresetTestKind.DpiFreeze || string.IsNullOrEmpty(CurrentPreset) || Targets.Count == 0)
            return;

        var clones = CloneTargets(TestTargetRowFormatter.DedupeByKey(Targets));
        UpsertScore(PresetScoreCalculator.FromDpiTargets(CurrentPreset, clones));
    }

    private bool HasTargetBreakdown(string fileName)
    {
        var normalized = NormalizeBat(fileName);
        foreach (var score in Scores)
        {
            if (!score.FileName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                continue;

            return score.Detail.Contains('\n') || score.Detail.Contains('\r');
        }

        return false;
    }

    private void TryFinalizeIfTargetsComplete()
    {
        if (string.IsNullOrEmpty(CurrentPreset) || _finalizedPresets.Contains(CurrentPreset))
            return;
        if (!AreAllTargetsComplete())
            return;
        FinalizeCurrentPreset();
    }

    private void FinalizeCurrentPresetIfNeeded()
    {
        if (string.IsNullOrEmpty(CurrentPreset))
            return;
        if (Targets.Count == 0)
            return;
        FinalizeCurrentPreset();
    }

    private void FinalizeCurrentPreset()
    {
        if (string.IsNullOrEmpty(CurrentPreset))
            return;

        var incoming = CloneTargets(TestTargetRowFormatter.DedupeByKey(Targets));
        if (incoming.Count == 0)
            return;

        if (_snapshots.TryGetValue(CurrentPreset, out var existing))
            incoming = MergeTargetRows(existing.Targets, incoming);

        _snapshots[CurrentPreset] = new PresetTargetSnapshot
        {
            FileName = CurrentPreset,
            DisplayName = CurrentPresetDisplay,
            Targets = incoming
        };
        _finalizedPresets.Add(CurrentPreset);

        var score = TestKind == PresetTestKind.DpiFreeze
            ? PresetScoreCalculator.FromDpiTargets(CurrentPreset, incoming)
            : PresetScoreCalculator.FromStandardTargets(CurrentPreset, incoming);
        UpsertScore(score);
    }

    private bool AreAllTargetsComplete()
    {
        if (Targets.Count == 0)
            return false;

        foreach (var row in Targets)
        {
            if (TestKind != PresetTestKind.DpiFreeze && row.Ping is "…")
                return false;

            if (!row.PingOnly)
            {
                if (row.Http is "HTTP:…" or "…")
                    return false;
                if (row.Tls12 is "TLS1.2:…" or "…")
                    return false;
                if (row.Tls13 is "TLS1.3:…" or "…")
                    return false;
            }
        }

        return true;
    }

    private static List<TestTargetRow> MergeTargetRows(
        IReadOnlyList<TestTargetRow> existing,
        IReadOnlyList<TestTargetRow> incoming)
    {
        var map = new Dictionary<string, TestTargetRow>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var row in existing)
        {
            var key = NormalizeTargetKey(row.Name);
            if (map.ContainsKey(key))
                continue;
            map[key] = row;
            order.Add(key);
        }

        foreach (var row in incoming)
        {
            var key = NormalizeTargetKey(row.Name);
            if (!map.ContainsKey(key))
                order.Add(key);
            map[key] = row;
        }

        return order.Select(key => map[key]).ToList();
    }

    private static List<TestTargetRow> CloneTargets(IEnumerable<TestTargetRow> rows) =>
        rows.Select(row => new TestTargetRow
        {
            Name = row.Name,
            PingOnly = row.PingOnly,
            Http = row.Http,
            Tls12 = row.Tls12,
            Tls13 = row.Tls13,
            Ping = row.Ping
        }).ToList();

    private void UpsertScore(PresetScoreRow score)
    {
        var fileName = NormalizeBat(score.FileName);
        var normalized = new PresetScoreRow
        {
            FileName = fileName,
            DisplayName = score.DisplayName,
            Detail = score.Detail,
            Glyph = score.Glyph,
            RankScore = score.RankScore,
            Fail = score.Fail,
            HttpOk = score.HttpOk
        };
        var existingIdx = -1;
        for (var i = 0; i < Scores.Count; i++)
        {
            if (Scores[i].FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            {
                existingIdx = i;
                break;
            }
        }

        if (existingIdx >= 0)
            Scores[existingIdx] = normalized;
        else
            Scores.Add(normalized);

        SortScores();
        Notify();
    }

    private void SeedStandardTargets()
    {
        Targets.Clear();
        _targetIndex.Clear();
        foreach (var def in _targetTemplate)
        {
            var row = new TestTargetRow
            {
                Name = def.Name,
                PingOnly = def.PingOnly,
                Http = "HTTP:…",
                Tls12 = "TLS1.2:…",
                Tls13 = "TLS1.3:…",
                Ping = "…"
            };
            Targets.Add(row);
            _targetIndex[NormalizeTargetKey(row.Name)] = row;
        }
        Notify();
    }

    private void SortScores()
    {
        var ordered = Scores.OrderByDescending(s => s.RankScore)
            .ThenBy(s => s.Fail)
            .ThenByDescending(s => s.HttpOk)
            .ToList();
        Scores.Clear();
        foreach (var s in ordered)
            Scores.Add(s);
    }

    private void FlushBuffer()
    {
        if (_lineBuffer.Length == 0) return;
        ProcessLine(NormalizePhysicalLine(_lineBuffer.ToString()));
        _lineBuffer.Clear();
    }

    private static string NormalizePhysicalLine(string line) => line.Trim();

    private void Notify() => Changed?.Invoke();

    private string ResolveCanonicalName(string rawName)
    {
        rawName = rawName.Trim();
        if (string.IsNullOrEmpty(rawName))
            return rawName;

        var cut = Regex.Match(rawName, @"^(.*?)(?:HTTP:|TLS1\.|Ping:|Пинг:)", RegexOptions.CultureInvariant);
        if (cut.Success && cut.Groups[1].Value.Trim().Length > 0)
            rawName = cut.Groups[1].Value.Trim();

        var leading = Regex.Match(rawName, @"^(\w+)");
        if (!leading.Success)
            return rawName;

        var key = NormalizeTargetKey(leading.Groups[1].Value);
        if (_templateNames.TryGetValue(key, out var templateName))
            return templateName;

        string? best = null;
        foreach (var kv in _templateNames)
        {
            if (!key.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase)
                && !kv.Key.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                continue;
            if (best is null || kv.Key.Length > best.Length)
                best = kv.Value;
        }

        return best ?? leading.Groups[1].Value;
    }

    private static Dictionary<string, string> BuildTemplateNameMap(IReadOnlyList<TestTargetDefinition> template)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in template)
            map[NormalizeTargetKey(def.Name)] = def.Name;
        return map;
    }

    private static string NormalizeTargetKey(string name) =>
        name.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    private static string NormalizeBat(string fileName)
    {
        if (!fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            fileName += ".bat";
        return fileName;
    }

    private static string StripAnsi(string text) =>
        Regex.Replace(text, @"\x1B(?:\][^\x07]*\x07|\[[\d;?]*[ -/]*[@-~])", "");

    public SavedTestKindState ExportKindState(string? reviewPresetFile) =>
        new()
        {
            TestKind = TestKind.ToString(),
            Targets = Targets.Select(SaveTarget).ToList(),
            Scores = Scores.Select(SaveScore).ToList(),
            Snapshots = _snapshots.Values.Select(SaveSnapshot).ToList(),
            CurrentPreset = CurrentPreset,
            CurrentPresetDisplay = CurrentPresetDisplay,
            StatusText = IsRunning ? Loc.T("tools.test_stopped") : StatusText,
            BestPresetFile = BestPresetFile,
            Progress = Progress,
            ProgressText = ProgressText,
            ReviewPresetFile = reviewPresetFile
        };

    public void ImportKindState(SavedTestKindState state)
    {
        if (IsRunning) return;

        Targets.Clear();
        Scores.Clear();
        _targetIndex.Clear();
        _snapshots.Clear();
        _finalizedPresets.Clear();
        _lineBuffer.Clear();
        _currentDpiTarget = null;
        _targetTemplate = [];
        _templateNames = [];

        if (Enum.TryParse<PresetTestKind>(state.TestKind, out var kind))
            TestKind = kind;

        foreach (var saved in state.Targets)
        {
            var row = LoadTarget(saved);
            Targets.Add(row);
            _targetIndex[NormalizeTargetKey(row.Name)] = row;
        }

        foreach (var saved in state.Scores)
            Scores.Add(LoadScore(saved));

        foreach (var saved in state.Snapshots)
        {
            var fileName = NormalizeBat(saved.FileName);
            _snapshots[fileName] = LoadSnapshot(saved);
            _finalizedPresets.Add(fileName);
        }

        CurrentPreset = state.CurrentPreset;
        CurrentPresetDisplay = state.CurrentPresetDisplay;
        StatusText = string.IsNullOrWhiteSpace(state.StatusText)
            ? Loc.T("tools.test_status_idle")
            : state.StatusText;
        BestPresetFile = state.BestPresetFile;
        Progress = state.Progress;
        ProgressText = state.ProgressText;
        IsRunning = false;
    }

    private static SavedTestTargetRow SaveTarget(TestTargetRow row) =>
        new()
        {
            Name = row.Name,
            Http = row.Http,
            Tls12 = row.Tls12,
            Tls13 = row.Tls13,
            Ping = row.Ping,
            PingOnly = row.PingOnly
        };

    private static TestTargetRow LoadTarget(SavedTestTargetRow row) =>
        new()
        {
            Name = row.Name,
            PingOnly = row.PingOnly,
            Http = row.Http,
            Tls12 = row.Tls12,
            Tls13 = row.Tls13,
            Ping = row.Ping
        };

    private static SavedPresetScoreRow SaveScore(PresetScoreRow row) =>
        new()
        {
            FileName = row.FileName,
            DisplayName = row.DisplayName,
            Detail = row.Detail,
            Glyph = row.Glyph,
            RankScore = row.RankScore,
            Fail = row.Fail,
            HttpOk = row.HttpOk
        };

    private static PresetScoreRow LoadScore(SavedPresetScoreRow row) =>
        new()
        {
            FileName = row.FileName,
            DisplayName = row.DisplayName,
            Detail = row.Detail,
            Glyph = row.Glyph,
            RankScore = row.RankScore,
            Fail = row.Fail,
            HttpOk = row.HttpOk
        };

    private static SavedPresetTargetSnapshot SaveSnapshot(PresetTargetSnapshot snapshot) =>
        new()
        {
            FileName = snapshot.FileName,
            DisplayName = snapshot.DisplayName,
            Targets = snapshot.Targets.Select(SaveTarget).ToList()
        };

    private static PresetTargetSnapshot LoadSnapshot(SavedPresetTargetSnapshot snapshot) =>
        new()
        {
            FileName = snapshot.FileName,
            DisplayName = snapshot.DisplayName,
            Targets = snapshot.Targets.Select(LoadTarget).ToList()
        };
}
