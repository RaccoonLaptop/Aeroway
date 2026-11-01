namespace Aeroway.Services;

/// <summary>
/// Сборка с бейджем DEV. Подбор стратегии открывается только отсюда.
/// </summary>
public static class AppBuild
{
    public static bool IsDevBuild
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }
}
