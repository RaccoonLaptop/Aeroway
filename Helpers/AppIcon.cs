using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Aeroway.Helpers;

public static class AppIcon
{
    private const int WmSetIcon = 0x0080;
    private const int IconSmall = 0;
    private const int IconBig = 1;

    private static readonly Uri PackUri = new("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
    private static ImageSource? _wpfIcon;
    private static System.Drawing.Icon? _drawingIcon;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);

    public static ImageSource WpfIcon => _wpfIcon ??= LoadWpfIcon();

    public static System.Drawing.Icon DrawingIcon => _drawingIcon ??= LoadDrawingIcon();

    public static void ApplyTo(Window window)
    {
        window.Icon = WpfIcon;
        if (window.IsLoaded)
            RefreshTaskbarIcon(window);
        else
            window.SourceInitialized += (_, _) => RefreshTaskbarIcon(window);
    }

    public static void RefreshTaskbarIcon(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var handle = DrawingIcon.Handle;
        SendMessage(hwnd, WmSetIcon, IconSmall, handle);
        SendMessage(hwnd, WmSetIcon, IconBig, handle);
    }

    private static ImageSource LoadWpfIcon()
    {
        var frame = BitmapFrame.Create(PackUri);
        frame.Freeze();
        return frame;
    }

    private static System.Drawing.Icon LoadDrawingIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(path))
            return new System.Drawing.Icon(path);

        var stream = Application.GetResourceStream(PackUri)?.Stream
            ?? throw new FileNotFoundException("Assets/app.ico not found");
        return new System.Drawing.Icon(stream);
    }
}
