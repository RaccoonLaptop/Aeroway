using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Aeroway.Services;

public static class LocalizationService
{
    private static Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
    private static string _language = "ru";

    public static string Language => _language;
    public static bool IsEnglish => _language == "en";

    public static bool RestartPending { get; private set; }

    public static void Initialize(string? language)
    {
        _language = NormalizeLanguage(language);
        var culture = new CultureInfo(_language == "en" ? "en-US" : "ru-RU");
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        LoadStrings(_language);
    }

    public static string T(string key) =>
        _strings.TryGetValue(key, out var value) ? value : key;

    public static string F(string key, params object[] args)
    {
        var template = T(key);
        try { return string.Format(template, args); }
        catch { return template; }
    }

    public static void RestartApplication()
    {
        RestartPending = true;
        System.Windows.Application.Current.Shutdown();
    }

    private static string NormalizeLanguage(string? language) =>
        string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ru";

    private static void LoadStrings(string language)
    {
        var suffix = language == "en" ? "en" : "ru";
        var fileName = $"Strings.{suffix}.json";

        foreach (var path in GetCandidatePaths(fileName))
        {
            if (!File.Exists(path)) continue;
            try
            {
                var json = File.ReadAllText(path);
                _strings = Deserialize(json);
                return;
            }
            catch { /* try next source */ }
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName is not null)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                _strings = Deserialize(reader.ReadToEnd());
                return;
            }
        }

        _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetCandidatePaths(string fileName)
    {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "Resources", fileName);

        var exeDir = Path.GetDirectoryName(Environment.ProcessPath ?? baseDir);
        if (!string.IsNullOrWhiteSpace(exeDir))
            yield return Path.Combine(exeDir, "Resources", fileName);

        var devPath = FindDevResourcePath(fileName);
        if (devPath is not null)
            yield return devPath;
    }

    private static string? FindDevResourcePath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent!)
        {
            var candidate = Path.Combine(dir.FullName, "Resources", fileName);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static Dictionary<string, string> Deserialize(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json)
        ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
