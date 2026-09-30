using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ZapretUI.Services;

/// <summary>
/// Смена темы — одна замена кистей. Покадровой перекраски нет.
/// Светлые HEX в репозитории не лежали: это та же золотая пара,
/// подогнанная так, чтобы вторичный текст на 12px проходил 4.5:1.
/// </summary>
public static class ThemeService
{
    public const string Dark = "dark";
    public const string Light = "light";
    public const string Windows = "windows";

    public static readonly Color LightPaper = Color.FromRgb(0xF6, 0xF1, 0xE6);
    public static readonly Color LightMuted = Color.FromRgb(0x5E, 0x53, 0x44);

    public static Color Backdrop { get; private set; } = Color.FromRgb(0x05, 0x0B, 0x1B);

    private static string _mode = Dark;
    private static int _watching;

    private static readonly Dictionary<string, Color> DarkColors = new()
    {
        ["BgBrush"] = Color.FromRgb(0x05, 0x0B, 0x1B),
        ["ShellOverlayBrush"] = Color.FromArgb(0xE6, 0x05, 0x0B, 0x1B),
        ["TitleBarOverlayBrush"] = Color.FromArgb(0xF2, 0x05, 0x0B, 0x1B),
        ["PanelOverlayBrush"] = Color.FromArgb(0xCC, 0x0D, 0x22, 0x40),
        ["SurfaceBrush"] = Color.FromRgb(0x0D, 0x22, 0x40),
        ["SurfaceLightBrush"] = Color.FromRgb(0x12, 0x35, 0x5B),
        ["SurfaceElevatedBrush"] = Color.FromRgb(0x1A, 0x40, 0x68),
        ["InputBrush"] = Color.FromRgb(0x0A, 0x1C, 0x34),
        ["BorderBrush"] = Color.FromRgb(0x2A, 0x4D, 0x73),
        ["AccentBrush"] = Color.FromRgb(0xE0, 0xB2, 0x40),
        ["AccentFillBrush"] = Color.FromRgb(0xE0, 0xB2, 0x40),
        ["AccentHoverBrush"] = Color.FromRgb(0xF0, 0xCC, 0x62),
        ["SuccessBrush"] = Color.FromRgb(0x8F, 0xD4, 0x60),
        ["WarningBrush"] = Color.FromRgb(0xE8, 0xB8, 0x6A),
        ["ErrorBrush"] = Color.FromRgb(0xF0, 0x70, 0x88),
        ["TextBrush"] = Color.FromRgb(0xF4, 0xE7, 0xC6),
        ["TextMutedBrush"] = Color.FromRgb(0xC9, 0xB8, 0x96),
        ["InkBrush"] = Color.FromRgb(0x05, 0x0B, 0x1B),
        ["CardBrush"] = Color.FromArgb(0x96, 0x10, 0x16, 0x28),
        ["CardSelectedBrush"] = Color.FromArgb(0xD2, 0x0C, 0x16, 0x28),
        ["CardBorderBrush"] = Color.FromArgb(0x50, 0x60, 0x70, 0x82),
        ["CardHoverBrush"] = Color.FromRgb(0x6E, 0x81, 0x96),
        ["SuccessFillBrush"] = Color.FromRgb(0x1A, 0x26, 0x14),
        ["StopFillBrush"] = Color.FromRgb(0x14, 0x26, 0x1C),
        ["BusyFillBrush"] = Color.FromRgb(0x2A, 0x26, 0x1C),
        ["BusyTextBrush"] = Color.FromRgb(0xC9, 0xB8, 0x96),
        ["SidebarBrush"] = Color.FromArgb(0x8C, 0x07, 0x10, 0x1C),
        ["LinksCardBrush"] = Color.FromArgb(0x80, 0x05, 0x0B, 0x1B),
        ["StatusCardBrush"] = Color.FromArgb(0xB0, 0x10, 0x18, 0x28),
        ["IconFaceBrush"] = Color.FromArgb(0x33, 0x10, 0x18, 0x28),
        ["IconEdgeBrush"] = Color.FromArgb(0x55, 0x3A, 0x52, 0x78),
        ["IconHoverBrush"] = Color.FromArgb(0x44, 0x12, 0x35, 0x5B),
        ["NavHoverBrush"] = Color.FromArgb(0x33, 0x12, 0x35, 0x5B)
    };

    private static readonly Dictionary<string, Color> LightColors = new()
    {
        ["BgBrush"] = LightPaper,
        ["ShellOverlayBrush"] = Color.FromArgb(0xE6, 0xF6, 0xF1, 0xE6),
        ["TitleBarOverlayBrush"] = Color.FromArgb(0xF2, 0xF6, 0xF1, 0xE6),
        ["PanelOverlayBrush"] = Color.FromRgb(0xFF, 0xFD, 0xF8),
        ["SurfaceBrush"] = Color.FromRgb(0xFF, 0xFD, 0xF8),
        ["SurfaceLightBrush"] = Color.FromRgb(0xEF, 0xE6, 0xD4),
        ["SurfaceElevatedBrush"] = Color.FromRgb(0xE4, 0xD8, 0xC2),
        ["InputBrush"] = Color.FromRgb(0xFF, 0xFD, 0xF8),
        ["BorderBrush"] = Color.FromRgb(0xC9, 0xB8, 0x96),
        ["AccentBrush"] = Color.FromRgb(0x7A, 0x5E, 0x10),
        ["AccentFillBrush"] = Color.FromRgb(0xE0, 0xB2, 0x40),
        ["AccentHoverBrush"] = Color.FromRgb(0xC4, 0x96, 0x2A),
        ["SuccessBrush"] = Color.FromRgb(0x2F, 0x6B, 0x32),
        ["WarningBrush"] = Color.FromRgb(0x8A, 0x5A, 0x10),
        ["ErrorBrush"] = Color.FromRgb(0xA3, 0x20, 0x40),
        ["TextBrush"] = Color.FromRgb(0x1C, 0x16, 0x08),
        ["TextMutedBrush"] = LightMuted,
        ["InkBrush"] = Color.FromRgb(0x1C, 0x16, 0x08),
        ["CardBrush"] = Color.FromRgb(0xFF, 0xFD, 0xF8),
        ["CardSelectedBrush"] = Color.FromRgb(0xFF, 0xF6, 0xE4),
        ["CardBorderBrush"] = Color.FromRgb(0xC9, 0xB8, 0x96),
        ["CardHoverBrush"] = Color.FromRgb(0x8A, 0x7A, 0x62),
        ["SuccessFillBrush"] = Color.FromRgb(0xE5, 0xF2, 0xE1),
        ["StopFillBrush"] = Color.FromRgb(0xE7, 0xF2, 0xE4),
        ["BusyFillBrush"] = Color.FromRgb(0xEF, 0xE6, 0xD4),
        ["BusyTextBrush"] = LightMuted,
        ["SidebarBrush"] = Color.FromArgb(0x99, 0xEF, 0xE6, 0xD4),
        ["LinksCardBrush"] = Color.FromArgb(0x80, 0xC9, 0xB8, 0x96),
        ["StatusCardBrush"] = Color.FromRgb(0xFF, 0xFD, 0xF8),
        ["IconFaceBrush"] = Color.FromRgb(0xFF, 0xF9, 0xF0),
        ["IconEdgeBrush"] = Color.FromRgb(0xC9, 0xB8, 0x96),
        ["IconHoverBrush"] = Color.FromRgb(0xE4, 0xD8, 0xC2),
        ["NavHoverBrush"] = Color.FromRgb(0xE4, 0xD8, 0xC2)
    };

    public static void Start()
    {
        if (Interlocked.Exchange(ref _watching, 1) == 1)
            return;
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
    }

    public static void Stop()
    {
        if (Interlocked.Exchange(ref _watching, 0) == 0)
            return;
        SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
    }

    public static void Apply(string? mode)
    {
        _mode = Normalize(mode);
        var colors = UsesLight(_mode) ? LightColors : DarkColors;
        var app = Application.Current;
        if (app is null)
            return;

        foreach (var pair in colors)
        {
            if (app.Resources[pair.Key] is SolidColorBrush brush && !brush.IsFrozen)
            {
                brush.Color = pair.Value;
                continue;
            }

            app.Resources[pair.Key] = new SolidColorBrush(pair.Value);
        }

        Backdrop = colors["BgBrush"];
    }

    private static void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_mode != Windows)
            return;
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            Apply(Windows);
            if (Application.Current?.MainWindow is global::ZapretUI.MainWindow window)
                window.ReloadActivePage();
        });
    }

    private static bool UsesLight(string mode) =>
        mode == Light || (mode == Windows && SystemUsesLightTheme());

    private static string Normalize(string? mode) =>
        mode switch
        {
            Light => Light,
            Windows => Windows,
            _ => Dark
        };

    private static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light != 0;
        }
        catch
        {
            return false;
        }
    }
}
