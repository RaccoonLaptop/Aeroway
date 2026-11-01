namespace Aeroway.Models;

public enum PresetTestKind
{
    Standard,
    DpiFreeze
}

public sealed class PresetTestScope
{
    public bool TestAll { get; init; } = true;
    public IReadOnlyList<string> SelectedStrategyFiles { get; init; } = Array.Empty<string>();
}

public sealed class TestTargetRow
{
    public required string Name { get; init; }
    public string Http { get; set; } = "HTTP:…";
    public string Tls12 { get; set; } = "TLS1.2:…";
    public string Tls13 { get; set; } = "TLS1.3:…";
    public string Ping { get; set; } = "…";
    public bool PingOnly { get; init; }
}

public sealed class PresetScoreRow
{
    public required string FileName { get; init; }
    public required string DisplayName { get; init; }
    public required string Detail { get; init; }
    public required string Glyph { get; init; }
    public int RankScore { get; init; }
    public int Fail { get; init; }
    public int HttpOk { get; init; }
}

public sealed class TestTargetDefinition
{
    public required string Name { get; init; }
    public bool PingOnly { get; init; }
}
