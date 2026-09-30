using ZapretUI.Controls.Backgrounds;

namespace ZapretUI.Services;

/// <summary>
/// Сообщает волнам, что обход включён.
/// Проверку discord.com и www.youtube.com по системному DNS не делаем:
/// браузер со своим DNS открывает их, даже если у системы DNS другой.
/// </summary>
public static class SitePulse
{
    public static void Tick(bool running) => WaveSignal.Running = running;
}
