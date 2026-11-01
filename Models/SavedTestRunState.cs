namespace Aeroway.Models;

public sealed class SavedTestRunSession
{
    public string LastSelectedKind { get; set; } = nameof(PresetTestKind.Standard);
    public SavedTestKindState Standard { get; set; } = new();
    public SavedTestKindState Dpi { get; set; } = new();
}

public sealed class SavedTestKindState
{
    public string TestKind { get; set; } = nameof(PresetTestKind.Standard);
    public List<SavedTestTargetRow> Targets { get; set; } = [];
    public List<SavedPresetScoreRow> Scores { get; set; } = [];
    public List<SavedPresetTargetSnapshot> Snapshots { get; set; } = [];
    public string CurrentPreset { get; set; } = "";
    public string CurrentPresetDisplay { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string BestPresetFile { get; set; } = "";
    public double Progress { get; set; }
    public string ProgressText { get; set; } = "";
    public string? ReviewPresetFile { get; set; }

    public bool HasContent => Targets.Count > 0 || Scores.Count > 0;
}

public sealed class SavedTestTargetRow
{
    public string Name { get; set; } = "";
    public string Http { get; set; } = "HTTP:…";
    public string Tls12 { get; set; } = "TLS1.2:…";
    public string Tls13 { get; set; } = "TLS1.3:…";
    public string Ping { get; set; } = "…";
    public bool PingOnly { get; set; }
}

public sealed class SavedPresetScoreRow
{
    public string FileName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Glyph { get; set; } = "";
    public int RankScore { get; set; }
    public int Fail { get; set; }
    public int HttpOk { get; set; }
}

public sealed class SavedPresetTargetSnapshot
{
    public string FileName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<SavedTestTargetRow> Targets { get; set; } = [];
}
