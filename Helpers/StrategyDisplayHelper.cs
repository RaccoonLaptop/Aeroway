using System.IO;
using System.Text.Json;

namespace Aeroway.Helpers;

public sealed class StrategyItem
{
    public required string FileName { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }

    public override string ToString() => DisplayName;
}

public static class StrategyDisplayHelper
{
    public static IReadOnlyList<StrategyItem> LoadItems(string zapretRoot, IEnumerable<string> batFiles)
    {
        return batFiles
            .Select(f => CreateItem(zapretRoot, f))
            .ToList();
    }

    public static StrategyItem CreateItem(string zapretRoot, string batFileName)
    {
        var displayName = ToDisplayName(batFileName);
        var description = TryGetJsonDescription(zapretRoot, batFileName);
        return new StrategyItem
        {
            FileName = batFileName,
            DisplayName = displayName,
            Description = description
        };
    }

    public static string ToDisplayName(string batFileName)
    {
        var name = Path.GetFileNameWithoutExtension(batFileName);
        return string.IsNullOrWhiteSpace(name) ? batFileName : name;
    }

    public static string GetHintText(StrategyItem? item)
    {
        if (item is null)
            return "";

        if (!string.IsNullOrWhiteSpace(item.Description))
            return item.Description;

        return Loc.F("home.preset_hint", item.DisplayName);
    }

    private static string? TryGetJsonDescription(string zapretRoot, string batFileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(batFileName);
        var jsonPath = Path.Combine(zapretRoot, baseName + ".json");
        if (!File.Exists(jsonPath))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            var root = doc.RootElement;
            foreach (var key in new[] { "description", "comment", "summary" })
            {
                if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }
            }
        }
        catch { /* ignore malformed sidecar */ }

        return null;
    }
}
