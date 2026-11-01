using System.IO;

namespace Aeroway.Services;

/// <summary>
/// Встроенные стратегии Aeroway (копируются в папку zapret при установке/запуске).
/// </summary>
public static class BundledStrategiesService
{
    public const string FeaturedStrategy = "Niko_ALT11.bat";

    private static readonly string[] Protected =
    [
        FeaturedStrategy
    ];

    public static string BundledDirectory =>
        Path.Combine(AppContext.BaseDirectory, "packaging", "strategies");

    public static bool IsProtected(string batFileName) =>
        Protected.Contains(batFileName, StringComparer.OrdinalIgnoreCase);

    public static void DeployTo(string zapretRoot)
    {
        if (!ZapretPaths.IsValidZapretRoot(zapretRoot))
            return;

        var sourceDir = BundledDirectory;
        if (!Directory.Exists(sourceDir))
            return;

        foreach (var sourceFile in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(sourceFile);
            if (string.IsNullOrEmpty(name))
                continue;

            var dest = Path.Combine(zapretRoot, name);
            try
            {
                File.Copy(sourceFile, dest, overwrite: true);
            }
            catch
            {
                // ignore locked files
            }
        }
    }
}
