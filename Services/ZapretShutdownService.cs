using System.Diagnostics;
using System.ServiceProcess;

namespace Aeroway.Services;

public static class ZapretShutdownService
{
    private static readonly string[] ServiceNames = ["zapret", "WinDivert", "WinDivert14"];

    public static void StopWinDivertServices()
    {
        foreach (var name in new[] { "WinDivert", "WinDivert14" })
        {
            try
            {
                using var svc = new ServiceController(name);
                if (svc.Status != ServiceControllerStatus.Running)
                    continue;

                svc.Stop();
                try { svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(3)); }
                catch { /* driver may linger briefly */ }
            }
            catch { /* service may not exist */ }
        }
    }

    public static void StopAll()
    {
        foreach (var proc in Process.GetProcessesByName("winws"))
        {
            try { proc.Kill(true); } catch { /* ignore */ }
        }

        foreach (var name in ServiceNames)
        {
            if (name is "WinDivert" or "WinDivert14")
                continue;

            try
            {
                using var svc = new ServiceController(name);
                if (svc.Status == ServiceControllerStatus.Running)
                    svc.Stop();
            }
            catch { /* service may not exist */ }
        }

        StopWinDivertServices();
    }

    public static bool IsWinDivertOrWinwsActive()
    {
        if (Process.GetProcessesByName("winws").Length > 0)
            return true;

        foreach (var name in ServiceNames)
        {
            try
            {
                using var svc = new ServiceController(name);
                if (svc.Status == ServiceControllerStatus.Running)
                    return true;
            }
            catch { /* ignore */ }
        }

        return false;
    }
}
