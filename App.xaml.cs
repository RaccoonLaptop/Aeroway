using System.Windows;
using ZapretUI.Helpers;
using ZapretUI.Services;

namespace ZapretUI;

public partial class App : Application
{
    private SingleInstanceService? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (InstallDirMigration.TryRelaunchIntoAerowayFolder())
        {
            Shutdown();
            return;
        }

        InstallDirMigration.RemoveLegacyExeCopy();
        InstallDirMigration.RemoveLeftoverLegacyFolder();

        var settings = AppSettings.Load();
        LocalizationService.Initialize(settings.Language);

        StartupArgs.Parse(e.Args);

        if (UpdateProgressLauncher.TryParseArgs(e.Args, out var progressLog, out var progressVersion))
        {
            var progressWin = new UpdateInstallProgressWindow(progressLog, progressVersion);
            MainWindow = progressWin;
            progressWin.Show();
            return;
        }

        _singleInstance = SingleInstanceService.Acquire(StartupArgs.WaitForSingleInstance);
        if (!_singleInstance.IsFirstInstance)
        {
            SingleInstanceService.TryActivateExisting();
            Shutdown();
            return;
        }

        WindowsAdmin.ClearForcedRunAs();
        AppStartupService.SyncWithSettings(settings.StartUiOnLogin);

        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    InstallDirMigration.FolderName);
                Directory.CreateDirectory(logDir);
                File.WriteAllText(
                    Path.Combine(logDir, "last-error.log"),
                    args.Exception.ToString());
            }
            catch { /* ignore */ }

            MessageBox.Show(
                LocalizationService.F("app.error_details", args.Exception.Message),
                LocalizationService.T("app.error_title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
            Shutdown();
        };

        if (!EnsureZapretInstalled(settings))
        {
            Shutdown();
            return;
        }

        var zapretRoot = ZapretPaths.DetectRoot(settings.ZapretRoot);
        BundledStrategiesService.DeployTo(zapretRoot);

        var main = new MainWindow(StartupArgs.StartInTray);
        MainWindow = main;
        main.Show();
        ScheduleFreshInstallDefaults(zapretRoot);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var restart = LocalizationService.RestartPending;
        var exe = Environment.ProcessPath;
        _singleInstance?.Dispose();
        base.OnExit(e);

        if (restart && !string.IsNullOrWhiteSpace(exe))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                Arguments = "--restart"
            });
        }
    }

    private static void ScheduleFreshInstallDefaults(string root)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await new ServiceDefaultsService(new ZapretPaths(root))
                    .ApplyFreshInstallDefaultsAsync()
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ConsoleLog.Instance.Write(LocalizationService.F("startup.error", ex.Message));
            }
        });
    }

    private static bool EnsureZapretInstalled(AppSettings settings)
    {
        var root = ZapretPaths.DetectRoot(settings.ZapretRoot);
        if (ZapretPaths.IsValidZapretRoot(root))
        {
            settings.ZapretRoot = root;
            settings.Save();
            BundledStrategiesService.DeployTo(root);
            return true;
        }

        var target = ZapretPaths.GetBundledZapretPath();
        var bootstrap = new BootstrapWindow(target);
        if (bootstrap.ShowDialog() != true)
            return false;

        if (!ZapretPaths.IsValidZapretRoot(target))
            return false;

        settings.ZapretRoot = target;
        settings.Save();
        BundledStrategiesService.DeployTo(target);
        return true;
    }
}
