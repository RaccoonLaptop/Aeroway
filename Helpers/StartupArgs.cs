namespace Aeroway.Helpers;

public static class StartupArgs
{
    public static bool StartInTray { get; private set; }
    public static bool WaitForSingleInstance { get; private set; }

    public static void Parse(string[]? args)
    {
        StartInTray = false;
        WaitForSingleInstance = false;
        if (args is null) return;

        foreach (var arg in args)
        {
            if (string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase))
                StartInTray = true;
            else if (string.Equals(arg, "--restart", StringComparison.OrdinalIgnoreCase))
                WaitForSingleInstance = true;
        }
    }
}
