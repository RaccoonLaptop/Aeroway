using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ZapretUI.Helpers;

/// <summary>
/// У окна без системной рамки разворот занимает весь монитор, включая панель задач.
/// WM_GETMINMAXINFO возвращает размер в пределах рабочей области.
/// </summary>
public static class MaximizedWorkArea
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 2;

    public static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(window) is not HwndSource source)
                return;

            source.AddHook(Hook);

            IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                OnMinMax(window, hwnd, msg, wParam, lParam, ref handled);
        };
    }

    private static IntPtr OnMinMax(Window window, IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmGetMinMaxInfo)
            return IntPtr.Zero;

        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
            return IntPtr.Zero;

        var monitorInfo = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return IntPtr.Zero;

        var work = monitorInfo.rcWork;
        var screen = monitorInfo.rcMonitor;
        info.ptMaxPosition.x = work.left - screen.left;
        info.ptMaxPosition.y = work.top - screen.top;
        info.ptMaxSize.x = work.right - work.left;
        info.ptMaxSize.y = work.bottom - work.top;

        var dpi = VisualTreeHelper.GetDpi(window);
        var minW = (int)Math.Ceiling(window.MinWidth * dpi.DpiScaleX);
        var minH = (int)Math.Ceiling(window.MinHeight * dpi.DpiScaleY);
        if (info.ptMinTrackSize.x < minW)
            info.ptMinTrackSize.x = minW;
        if (info.ptMinTrackSize.y < minH)
            info.ptMinTrackSize.y = minH;

        Marshal.StructureToPtr(info, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point ptReserved;
        public Point ptMaxSize;
        public Point ptMaxPosition;
        public Point ptMinTrackSize;
        public Point ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public int dwFlags;
    }
}
