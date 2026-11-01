using System.Security.Principal;
using Microsoft.Win32;

namespace Aeroway.Services;

public static class WindowsAdmin
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Установщик раньше писал RUNASADMIN, из-за этого окно всегда просило права администратора.
    /// </summary>
    public static void ClearForcedRunAs()
    {
        var exe = AppStartupService.ResolveExecutablePath();
        if (string.IsNullOrEmpty(exe))
            return;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers",
                writable: true);
            key?.DeleteValue(exe, throwOnMissingValue: false);
        }
        catch
        {
            /* ключ недоступен */
        }
    }
}
