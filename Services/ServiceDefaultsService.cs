using System.IO;

namespace Aeroway.Services;

public sealed class ServiceDefaultsService
{
    private const string MarkerFileName = ".zapretui_defaults_v1";

    private readonly ZapretPaths _paths;
    private readonly ServiceSettingsService _settings;
    private readonly UpdateService _updates;

    public ServiceDefaultsService(ZapretPaths paths)
    {
        _paths = paths;
        _settings = new ServiceSettingsService(paths);
        _updates = new UpdateService(paths);
    }

    public async Task ApplyFreshInstallDefaultsAsync(CancellationToken ct = default)
    {
        if (!_paths.IsValid) return;

        var marker = Path.Combine(_paths.Utils, MarkerFileName);
        if (File.Exists(marker)) return;

        Directory.CreateDirectory(_paths.Utils);
        _settings.SetGameFilter("all");

        try
        {
            await _updates.UpdateIpsetListAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // Keep going: game filter default is still useful even if IPSet download fails offline.
        }

        await File.WriteAllTextAsync(marker, DateTime.UtcNow.ToString("O"), ct).ConfigureAwait(false);
    }
}
