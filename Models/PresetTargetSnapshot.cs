namespace Aeroway.Models;

public sealed class PresetTargetSnapshot
{
    public required string FileName { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<TestTargetRow> Targets { get; init; }
}
