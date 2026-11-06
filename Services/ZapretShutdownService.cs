using System.Diagnostics;
using System.ServiceProcess;

namespace Aeroway.Services;

public static class ZapretShutdownService
{
    internal static readonly string[] DriverServiceNames = ["WinDivert", "WinDivert14", "Monkey"];

    private static readonly string[] ServiceNames = ["zapret", "WinDivert", "WinDivert14", "Monkey"];

    public static void StopWinDivertServices()
    {
        foreach (var name in DriverServiceNames)
            TryStopService(name);
    }

    public static void StopAll()
    {
        KillWinws();

        foreach (var name in ServiceNames)
        {
            if (DriverServiceNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                continue;

            TryStopService(name);
        }

        StopWinDivertServices();
    }

    /// <summary>
    /// Останавливает winws и zapret, затем останавливает и удаляет службы WinDivert, WinDivert14 и Monkey,
    /// чтобы файлы драйвера можно было заменить.
    /// </summary>
    public static void PrepareForDriverFileSwap()
    {
        KillWinws();
        TryStopService("zapret");
        foreach (var name in DriverServiceNames)
        {
            TryStopService(name);
            RunSc("stop", name);
            RunSc("delete", name);
        }

        Thread.Sleep(300);
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
            catch
            {
                /* service may not exist */
            }
        }

        return false;
    }

    private static void KillWinws()
    {
        foreach (var proc in Process.GetProcessesByName("winws"))
        {
            try { proc.Kill(true); } catch { /* ignore */ }
        }

        for (var i = 0; i < 20; i++)
        {
            if (Process.GetProcessesByName("winws").Length == 0)
                break;
            Thread.Sleep(100);
        }
    }

    private static void TryStopService(string name)
    {
        try
        {
            using var svc = new ServiceController(name);
            if (svc.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
                return;

            svc.Stop();
            try { svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(3)); }
            catch { /* driver may linger briefly */ }
        }
        catch
        {
            /* service may not exist */
        }
    }

    private static void RunSc(string verb, string serviceName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add(verb);
            psi.ArgumentList.Add(serviceName);
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
        }
        catch
        {
            /* sc is unavailable */
        }
    }
}
