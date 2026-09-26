using System.IO;
using System.Text.Json;

namespace ZapretUI.Services;

public sealed class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        InstallDirMigration.FolderName, "settings.json");

    public string? ZapretRoot { get; set; }
    public string? LastStrategy { get; set; }
    public bool MinimizeToTray { get; set; }
    /// <summary>Проверять Aeroway и Flowseal при запуске (только с подтверждением пользователя).</summary>
    public bool CheckUpdatesOnStartup { get; set; } = true;
    /// <summary>Устарело: авто-установка без подтверждения отключена.</summary>
    public bool AutoUpdateApp { get; set; } = false;
    public string? UpdateManifestUrl { get; set; }
    public string? LastInstalledVersion { get; set; }
    /// <summary>ID фона главной страницы (см. HomeBackgroundCatalog).</summary>
    public string HomeBackground { get; set; } = "wavy";
    /// <summary>Язык интерфейса: ru или en.</summary>
    public string Language { get; set; } = "ru";
    /// <summary>Автозапуск: Aeroway в трее и последний выбранный пресет при входе в Windows.</summary>
    public bool StartUiOnLogin { get; set; }
    /// <summary>Устарело: объединено с StartUiOnLogin.</summary>
    public bool StartBypassOnLogin { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool WindowMaximized { get; set; }

    public static string? LoadError { get; private set; }

    private static void MigrateLegacySettings()
    {
        if (File.Exists(SettingsPath))
            return;
        var legacy = Path.Combine(InstallDirMigration.LegacyDataDirectory, "settings.json");
        if (!File.Exists(legacy))
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.Copy(legacy, SettingsPath, overwrite: false);
    }

    public static AppSettings Load()
    {
        LoadError = null;
        try
        {
            MigrateLegacySettings();
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                // Раньше AutoUpdateApp включал установку без спроса — теперь только проверка.
                if (settings.AutoUpdateApp && !settings.CheckUpdatesOnStartup)
                    settings.CheckUpdatesOnStartup = true;
                settings.AutoUpdateApp = false;
                if (settings.StartBypassOnLogin)
                {
                    settings.StartUiOnLogin = true;
                    settings.StartBypassOnLogin = false;
                }
                // Минимизация всегда в панель задач; настройка больше не используется.
                settings.MinimizeToTray = false;
                return settings;
            }
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
        }
        return new AppSettings();
    }

    public void Save()
    {
        Language = LocalizationService.Language;
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
