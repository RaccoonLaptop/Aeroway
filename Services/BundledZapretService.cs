using System.IO;

namespace Aeroway.Services;

/// <summary>
/// Pre-bundled Flowseal/zapret package shipped with Aeroway (offline install fallback).
/// </summary>
public static class BundledZapretService
{
    public static string BundledDirectory =>
        Path.Combine(AppContext.BaseDirectory, "packaging", "zapret");

    public static bool HasBundledCopy() => ZapretPaths.IsValidZapretRoot(BundledDirectory);

    public static string GetBundledVersion()
    {
        if (!HasBundledCopy())
            return "";

        return new ZapretPaths(BundledDirectory).GetLocalVersion();
    }

    public static bool TryDeployTo(string targetDir, out string message)
    {
        message = "";
        if (!HasBundledCopy())
        {
            message = "Bundled zapret package is missing.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(targetDir);
            CopyTree(BundledDirectory, targetDir);

            if (!ZapretPaths.IsValidZapretRoot(targetDir))
            {
                message = "Bundled zapret package is incomplete.";
                return false;
            }

            BundledStrategiesService.DeployTo(targetDir);
            var version = new ZapretPaths(targetDir).GetLocalVersion();
            message = string.IsNullOrWhiteSpace(version)
                ? "Installed bundled zapret."
                : $"Installed bundled zapret {version}.";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private static void CopyTree(string sourceDir, string targetDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(targetDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, true);
        }
    }
}
