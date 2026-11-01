using System.IO;

namespace Aeroway.Services;

public static class TestTargetsService
{
    public static void EnsureTargetsFile(ZapretPaths paths)
    {
        if (!paths.IsValid) return;

        var dest = Path.Combine(paths.Utils, "targets.txt");
        if (File.Exists(dest)) return;

        var bundled = Path.Combine(AppContext.BaseDirectory, "Scripts", "targets.txt");
        if (!File.Exists(bundled)) return;

        Directory.CreateDirectory(paths.Utils);
        File.Copy(bundled, dest);
    }
}
