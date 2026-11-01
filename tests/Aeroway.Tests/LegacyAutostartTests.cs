using Aeroway.Services;
using Xunit;

namespace Aeroway.Tests;

public class LegacyAutostartTests
{
    [Fact]
    public void Legacy_task_or_run_key_still_counts_as_enabled()
    {
        var store = new FakeStartup();
        Assert.False(AppStartupService.IsEnabled(store));

        store.Tasks.Add("ZapretUI_Autostart");
        Assert.True(AppStartupService.IsEnabled(store));

        store.Tasks.Clear();
        store.Tasks.Add("Aeroway_Autostart");
        Assert.True(AppStartupService.IsEnabled(store));

        store.Tasks.Clear();
        store.Run["ZapretUI"] = "\"C:\\Old\\ZapretUI.exe\" --tray";
        Assert.True(AppStartupService.IsEnabled(store));

        store.Run.Clear();
        store.Run["Aeroway"] = "\"C:\\New\\Aeroway.exe\" --tray";
        Assert.True(AppStartupService.IsEnabled(store));
    }

    [Fact]
    public void Enable_replaces_the_legacy_task_and_run_value()
    {
        var store = new FakeStartup { RegisterSucceeds = true };
        store.Tasks.Add("ZapretUI_Autostart");
        store.Run["ZapretUI"] = "\"C:\\Old\\ZapretUI.exe\" --tray";
        store.Run["Aeroway"] = "stale";
        var exe = "C:\\Users\\tester\\AppData\\Local\\Aeroway\\Aeroway.exe";

        AppStartupService.Enable(store, exe);

        Assert.Equal(new[] { "Aeroway_Autostart", "ZapretUI_Autostart" }, store.DeletedTasks);
        Assert.Equal(exe, Assert.Single(store.Registered));
        Assert.Empty(store.Run);
    }

    [Fact]
    public void Enable_falls_back_to_the_aeroway_run_key_when_the_task_cannot_be_registered()
    {
        var store = new FakeStartup { RegisterSucceeds = false };
        store.Run["ZapretUI"] = "\"C:\\Old\\ZapretUI.exe\" --tray";
        var exe = "C:\\Aeroway\\Aeroway.exe";

        AppStartupService.Enable(store, exe);

        Assert.Equal(new[] { "Aeroway_Autostart", "ZapretUI_Autostart" }, store.DeletedTasks);
        Assert.Equal($"\"{exe}\" --tray", store.Run["Aeroway"]);
        Assert.Equal("\"C:\\Old\\ZapretUI.exe\" --tray", store.Run["ZapretUI"]);
    }

    [Fact]
    public void Disable_removes_legacy_and_current_autostart()
    {
        var store = new FakeStartup();
        store.Tasks.Add("ZapretUI_Autostart");
        store.Tasks.Add("Aeroway_Autostart");
        store.Run["ZapretUI"] = "old";
        store.Run["Aeroway"] = "new";

        AppStartupService.Disable(store);

        Assert.Equal(new[] { "Aeroway_Autostart", "ZapretUI_Autostart" }, store.DeletedTasks);
        Assert.Empty(store.Tasks);
        Assert.Empty(store.Run);
    }

    [Fact]
    public void Executable_resolution_uses_the_running_process_then_the_compat_copy()
    {
        using var tree = new TempTree();
        var dir = tree.Combine("app");
        Directory.CreateDirectory(dir);
        var legacy = Path.Combine(dir, "ZapretUI.exe");
        var current = Path.Combine(dir, "Aeroway.exe");
        File.WriteAllText(legacy, "old");
        File.WriteAllText(current, "new");

        Assert.Equal(current, AppStartupService.ResolveExecutablePath(current, dir));
        Assert.Equal(current, AppStartupService.ResolveExecutablePath(Path.Combine(dir, "missing.exe"), dir));
        File.Delete(current);
        Assert.Equal(legacy, AppStartupService.ResolveExecutablePath(null, dir));
    }

    private sealed class FakeStartup : IStartupStore
    {
        public HashSet<string> Tasks { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> DeletedTasks { get; } = [];
        public List<string> Registered { get; } = [];
        public Dictionary<string, string> Run { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool RegisterSucceeds { get; init; } = true;

        public bool TaskExists(string name) => Tasks.Contains(name);

        public void DeleteTask(string name)
        {
            DeletedTasks.Add(name);
            Tasks.Remove(name);
        }

        public bool TryRegisterAutostartTask(string exePath)
        {
            if (!RegisterSucceeds)
                return false;
            Registered.Add(exePath);
            Tasks.Add("Aeroway_Autostart");
            return true;
        }

        public string? ReadRunValue(string name) => Run.TryGetValue(name, out var value) ? value : null;

        public void WriteRunValue(string name, string value) => Run[name] = value;

        public void DeleteRunValue(string name) => Run.Remove(name);
    }
}
