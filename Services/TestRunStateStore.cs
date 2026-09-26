using System.IO;
using System.Text.Json;
using ZapretUI.Models;

namespace ZapretUI.Services;

public static class TestRunStateStore
{
    private static readonly string SessionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        InstallDirMigration.FolderName, "test-session.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static SavedTestRunSession Load()
    {
        try
        {
            if (!File.Exists(SessionPath))
                return new SavedTestRunSession();

            var json = File.ReadAllText(SessionPath);
            return JsonSerializer.Deserialize<SavedTestRunSession>(json, JsonOptions) ?? new SavedTestRunSession();
        }
        catch
        {
            return new SavedTestRunSession();
        }
    }

    public static void Save(SavedTestRunSession session)
    {
        try
        {
            var dir = Path.GetDirectoryName(SessionPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(session, JsonOptions);
            File.WriteAllText(SessionPath, json);
        }
        catch
        {
            // ignore persistence errors
        }
    }
}
