using System.Reflection;
using Aeroway.Services;
using Xunit;

namespace Aeroway.Tests;

public class ZapretUiMigrationTests
{
    [Fact]
    public void Relaunch_copies_zapretui_install_and_cleans_old_shortcuts()
    {
        using var tree = new TempTree();
        var local = tree.Combine("local");
        var programs = tree.Combine("programs");
        var desktop = tree.Combine("desktop");
        var install = Path.Combine(local, "ZapretUI");
        Directory.CreateDirectory(Path.Combine(install, "Scripts"));
        Directory.CreateDirectory(Path.Combine(install, "sub"));
        Directory.CreateDirectory(Path.Combine(programs, "Zapret UI"));
        Directory.CreateDirectory(desktop);
        File.WriteAllText(Path.Combine(install, "Aeroway.exe"), "new");
        File.WriteAllText(Path.Combine(install, "ZapretUI.exe"), "old");
        File.WriteAllText(Path.Combine(install, "settings.json"), "{\"Language\":\"ru\"}");
        File.WriteAllText(Path.Combine(install, "sub", "note.txt"), "kept");
        File.WriteAllText(Path.Combine(install, "locked.dat"), "busy");
        File.WriteAllText(Path.Combine(install, "Scripts", "remove-legacy-zapretui.ps1"), "# test");
        File.WriteAllText(Path.Combine(programs, "Zapret UI.lnk"), "old-start");
        File.WriteAllText(Path.Combine(programs, "Zapret UI", "extra.lnk"), "old-folder");
        File.WriteAllText(Path.Combine(desktop, "Zapret UI.lnk"), "old-desktop");
        File.WriteAllText(Path.Combine(desktop, "Aeroway.lnk"), "rewrite-me");

        var registry = new MemoryUninstallKey();
        registry.Values["UninstallString"] = $"\"{install}\\Aeroway.exe\" /uninstall";
        registry.Values["DisplayIcon"] = install + "\\ZapretUI.exe";
        registry.Values["Quiet"] = 1;
        registry.Values["Publisher"] = "leave-this";
        var effects = new RecordingEffects { Key = registry };

        using var locked = new FileStream(
            Path.Combine(install, "locked.dat"),
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        var migrated = InstallDirMigration.TryRelaunchIntoAerowayFolder(new MigrationEnvironment
        {
            BaseDirectory = install,
            ProcessPath = Path.Combine(install, "Aeroway.exe"),
            ProcessId = 4242,
            LocalAppData = local,
            ProgramsDirectory = programs,
            DesktopDirectory = desktop,
            CommandLineArgs = ["--tray"],
            Effects = effects
        });

        Assert.True(migrated);
        var dest = Path.Combine(local, "Aeroway");
        Assert.Equal("kept", File.ReadAllText(Path.Combine(dest, "sub", "note.txt")));
        Assert.Equal("{\"Language\":\"ru\"}", File.ReadAllText(Path.Combine(dest, "settings.json")));
        Assert.False(File.Exists(Path.Combine(dest, "locked.dat")));
        Assert.True(File.Exists(Path.Combine(install, "settings.json")));

        var started = Assert.Single(effects.Starts);
        Assert.Equal(Path.Combine(dest, "Aeroway.exe"), started.Exe);
        Assert.Equal(dest, started.WorkingDirectory);
        Assert.Equal(new[] { "--tray" }, started.Arguments);

        Assert.Equal($"\"{dest}\\Aeroway.exe\" /uninstall", registry.Values["UninstallString"]);
        Assert.Equal(Path.Combine(dest, "Aeroway.exe"), registry.Values["DisplayIcon"]);
        Assert.Equal("Aeroway", registry.Values["DisplayName"]);
        Assert.Equal(dest + "\\", registry.Values["InstallLocation"]);
        Assert.Equal(1, registry.Values["Quiet"]);
        Assert.Equal("leave-this", registry.Values["Publisher"]);

        Assert.False(File.Exists(Path.Combine(programs, "Zapret UI.lnk")));
        Assert.False(Directory.Exists(Path.Combine(programs, "Zapret UI")));
        Assert.False(File.Exists(Path.Combine(desktop, "Zapret UI.lnk")));
        Assert.Equal(Path.Combine(dest, "Aeroway.exe"), ShortcutTarget(Path.Combine(programs, "Aeroway.lnk")), ignoreCase: true);
        Assert.Equal(Path.Combine(dest, "Aeroway.exe"), ShortcutTarget(Path.Combine(desktop, "Aeroway.lnk")), ignoreCase: true);

        var scheduled = Assert.Single(effects.PowerShell);
        Assert.Contains(Path.Combine(install, "Scripts", "remove-legacy-zapretui.ps1"), scheduled, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(install, scheduled, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-WaitPid 4242", scheduled, StringComparison.Ordinal);
        locked.Dispose();
    }

    [Fact]
    public void Relaunch_uses_zapretui_exe_when_aeroway_exe_is_absent()
    {
        using var tree = new TempTree();
        var local = tree.Combine("local");
        var install = Path.Combine(local, "zapretui");
        Directory.CreateDirectory(install);
        Directory.CreateDirectory(tree.Combine("programs"));
        Directory.CreateDirectory(tree.Combine("desktop"));
        File.WriteAllText(Path.Combine(install, "ZapretUI.exe"), "old");
        var effects = new RecordingEffects();

        var migrated = InstallDirMigration.TryRelaunchIntoAerowayFolder(new MigrationEnvironment
        {
            BaseDirectory = install + "\\",
            ProcessPath = Path.Combine(install, "ZapretUI.exe"),
            ProcessId = 7,
            LocalAppData = local,
            ProgramsDirectory = tree.Combine("programs"),
            DesktopDirectory = tree.Combine("desktop"),
            Effects = effects
        });

        Assert.True(migrated);
        Assert.Equal(
            Path.Combine(local, "Aeroway", "ZapretUI.exe"),
            Assert.Single(effects.Starts).Exe);
        Assert.False(File.Exists(Path.Combine(tree.Combine("desktop"), "Aeroway.lnk")));
    }

    [Fact]
    public void Relaunch_ignores_an_install_that_is_already_aeroway()
    {
        using var tree = new TempTree();
        var install = tree.Combine("local", "Aeroway");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "Aeroway.exe"), "new");
        var effects = new RecordingEffects();

        var migrated = InstallDirMigration.TryRelaunchIntoAerowayFolder(new MigrationEnvironment
        {
            BaseDirectory = install,
            LocalAppData = tree.Combine("local"),
            ProgramsDirectory = tree.Combine("programs"),
            DesktopDirectory = tree.Combine("desktop"),
            Effects = effects
        });

        Assert.False(migrated);
        Assert.Empty(effects.Starts);
    }

    [Fact]
    public void Relaunch_stops_when_the_copied_folder_has_no_executable()
    {
        using var tree = new TempTree();
        var local = tree.Combine("local");
        var install = Path.Combine(local, "ZapretUI");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "readme.txt"), "no exe");
        var effects = new RecordingEffects();

        var migrated = InstallDirMigration.TryRelaunchIntoAerowayFolder(new MigrationEnvironment
        {
            BaseDirectory = install,
            LocalAppData = local,
            ProgramsDirectory = tree.Combine("programs"),
            DesktopDirectory = tree.Combine("desktop"),
            Effects = effects
        });

        Assert.False(migrated);
        Assert.Empty(effects.Starts);
        Assert.Empty(effects.PowerShell);
        Assert.Equal("no exe", File.ReadAllText(Path.Combine(local, "Aeroway", "readme.txt")));
    }

    [Fact]
    public void Legacy_exe_copy_is_removed_only_after_aeroway_exe_is_the_running_process()
    {
        using var tree = new TempTree();
        var install = tree.Combine("app");
        Directory.CreateDirectory(install);
        var aeroway = Path.Combine(install, "Aeroway.exe");
        var legacy = Path.Combine(install, "ZapretUI.exe");
        File.WriteAllText(aeroway, "new");
        File.WriteAllText(legacy, "old");

        InstallDirMigration.RemoveLegacyExeCopy(Env(install, processPath: null));
        Assert.True(File.Exists(legacy));

        InstallDirMigration.RemoveLegacyExeCopy(Env(install, legacy));
        Assert.True(File.Exists(legacy));

        InstallDirMigration.RemoveLegacyExeCopy(Env(install, aeroway));
        Assert.False(File.Exists(legacy));
        Assert.True(File.Exists(aeroway));
    }

    [Fact]
    public void First_existing_exe_prefers_aeroway_then_zapretui()
    {
        using var tree = new TempTree();
        var dir = tree.Combine("app");
        Directory.CreateDirectory(dir);
        Assert.Null(InstallDirMigration.FirstExistingExe(dir));

        File.WriteAllText(Path.Combine(dir, "ZapretUI.exe"), "old");
        Assert.Equal(Path.Combine(dir, "ZapretUI.exe"), InstallDirMigration.FirstExistingExe(dir));

        File.WriteAllText(Path.Combine(dir, "Aeroway.exe"), "new");
        Assert.Equal(Path.Combine(dir, "Aeroway.exe"), InstallDirMigration.FirstExistingExe(dir));
    }

    [Fact]
    public void Leftover_zapretui_folder_is_scheduled_from_the_new_install()
    {
        using var tree = new TempTree();
        var local = tree.Combine("local");
        var legacy = Path.Combine(local, "ZapretUI");
        var current = Path.Combine(local, "Aeroway");
        Directory.CreateDirectory(Path.Combine(current, "Scripts"));
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(current, "Scripts", "remove-legacy-zapretui.ps1"), "# test");
        var effects = new RecordingEffects();
        var env = new MigrationEnvironment
        {
            BaseDirectory = current,
            ProcessPath = Path.Combine(current, "Aeroway.exe"),
            ProcessId = 99,
            LocalAppData = local,
            ProgramsDirectory = tree.Combine("programs"),
            DesktopDirectory = tree.Combine("desktop"),
            Effects = effects
        };

        InstallDirMigration.RemoveLeftoverLegacyFolder(env, bundledZapretValid: false);
        Assert.Empty(effects.PowerShell);

        InstallDirMigration.RemoveLeftoverLegacyFolder(env, bundledZapretValid: true);
        var scheduled = Assert.Single(effects.PowerShell);
        Assert.Contains("-WaitPid 0", scheduled, StringComparison.Ordinal);
        Assert.Contains(legacy, scheduled, StringComparison.OrdinalIgnoreCase);

        effects.PowerShell.Clear();
        InstallDirMigration.RemoveLeftoverLegacyFolder(env with
        {
            BaseDirectory = legacy,
            ProcessPath = Path.Combine(legacy, "ZapretUI.exe")
        }, bundledZapretValid: true);
        Assert.Empty(effects.PowerShell);
    }

    [Fact]
    public void Settings_are_copied_from_the_zapretui_data_folder_once()
    {
        using var tree = new TempTree();
        var legacy = tree.Combine("ZapretUI", "settings.json");
        var current = tree.Combine("Aeroway", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, "{\"Language\":\"en\"}");

        Assert.True(AppSettings.TryCopyLegacySettings(current, legacy));
        Assert.Equal("{\"Language\":\"en\"}", File.ReadAllText(current));
        File.WriteAllText(legacy, "{\"Language\":\"ru\"}");
        Assert.False(AppSettings.TryCopyLegacySettings(current, legacy));
        Assert.Equal("{\"Language\":\"en\"}", File.ReadAllText(current));
        Assert.False(AppSettings.TryCopyLegacySettings(tree.Combine("missing", "settings.json"), tree.Combine("nope.json")));
    }

    [Fact]
    public void Saved_zapret_root_inside_the_old_data_folder_is_legacy()
    {
        var legacy = Path.Combine("C:\\Users\\tester\\AppData\\Local", "ZapretUI");
        Assert.True(ZapretPaths.IsLegacyInstallPath(legacy, legacy));
        Assert.True(ZapretPaths.IsLegacyInstallPath(Path.Combine(legacy, "zapret"), legacy));
        Assert.False(ZapretPaths.IsLegacyInstallPath(Path.Combine(legacy + "-other", "zapret"), legacy));
        Assert.False(ZapretPaths.IsLegacyInstallPath(null, legacy));
    }

    private static MigrationEnvironment Env(string install, string? processPath) => new()
    {
        BaseDirectory = install,
        ProcessPath = processPath,
        LocalAppData = install,
        ProgramsDirectory = install,
        DesktopDirectory = install,
        Effects = new RecordingEffects()
    };

    private static string ShortcutTarget(string path)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        Assert.NotNull(shellType);
        var shell = Activator.CreateInstance(shellType!)!;
        var shortcut = shellType!.InvokeMember(
            "CreateShortcut",
            BindingFlags.InvokeMethod,
            binder: null,
            shell,
            new object[] { path });
        Assert.NotNull(shortcut);
        var target = shortcut!.GetType().InvokeMember(
            "TargetPath",
            BindingFlags.GetProperty,
            binder: null,
            shortcut,
            args: null) as string;
        return target ?? "";
    }

    private sealed class RecordingEffects : IMigrationEffects
    {
        public MemoryUninstallKey? Key { get; init; }
        public List<Launch> Starts { get; } = [];
        public List<string> PowerShell { get; } = [];

        public IUninstallRegistryKey? OpenUninstallKey() => Key;

        public void StartProcess(string exe, string workingDirectory, IReadOnlyList<string> arguments) =>
            Starts.Add(new Launch(exe, workingDirectory, arguments.ToArray()));

        public void StartHiddenPowerShell(string arguments) => PowerShell.Add(arguments);

        public sealed record Launch(string Exe, string WorkingDirectory, string[] Arguments);
    }

    private sealed class MemoryUninstallKey : IUninstallRegistryKey
    {
        public Dictionary<string, object> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public IEnumerable<string> ValueNames => Values.Keys;
        public object? GetValue(string name) => Values.TryGetValue(name, out var value) ? value : null;
        public void SetValue(string name, object value) => Values[name] = value;
        public void Dispose() { }
    }
}
