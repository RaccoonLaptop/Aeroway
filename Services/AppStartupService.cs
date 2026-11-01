using System.Security.Principal;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

namespace Aeroway.Services;

public static class AppStartupService
{
    private const string TaskName = "Aeroway_Autostart";
    private const string LegacyTaskName = "ZapretUI_Autostart";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Aeroway";
    private const string LegacyValueName = "ZapretUI";

    public static bool IsEnabled()
    {
        try
        {
            using var taskService = new TaskService();
            return IsEnabled(new WindowsStartupStore(taskService));
        }
        catch
        {
            return IsEnabled(new WindowsStartupStore(null));
        }
    }

    internal static bool IsEnabled(IStartupStore store)
    {
        try
        {
            if (store.TaskExists(TaskName) || store.TaskExists(LegacyTaskName))
                return true;
        }
        catch
        {
            /* task scheduler unavailable */
        }

        return HasRunValue(store);
    }

    public static void Enable()
    {
        var exe = ResolveExecutablePath();
        if (exe is null) return;

        try
        {
            using var taskService = new TaskService();
            Enable(new WindowsStartupStore(taskService), exe);
        }
        catch
        {
            Enable(new WindowsStartupStore(null), exe);
        }
    }

    internal static void Enable(IStartupStore store, string exe)
    {
        try
        {
            store.DeleteTask(TaskName);
            store.DeleteTask(LegacyTaskName);
            if (store.TryRegisterAutostartTask(exe))
            {
                RemoveRunValues(store);
                return;
            }
        }
        catch
        {
            /* task scheduler refused the registration */
        }

        store.WriteRunValue(ValueName, $"\"{exe}\" --tray");
    }

    public static void Disable()
    {
        TaskService? taskService = null;
        try
        {
            taskService = new TaskService();
        }
        catch
        {
            taskService = null;
        }

        try
        {
            Disable(new WindowsStartupStore(taskService));
        }
        finally
        {
            taskService?.Dispose();
        }
    }

    internal static void Disable(IStartupStore store)
    {
        try
        {
            store.DeleteTask(TaskName);
            store.DeleteTask(LegacyTaskName);
        }
        catch
        {
            /* not registered */
        }

        RemoveRunValues(store);
    }

    /// <summary>Синхронизирует автозапуск UI с настройкой StartUiOnLogin.</summary>
    public static void SyncWithSettings(bool startOnLogin)
    {
        if (startOnLogin)
            Enable();
        else
            Disable();
    }

    public static string? ResolveExecutablePath() =>
        ResolveExecutablePath(Environment.ProcessPath, AppContext.BaseDirectory);

    internal static string? ResolveExecutablePath(string? processPath, string baseDirectory)
    {
        if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            return processPath;

        return InstallDirMigration.FirstExistingExe(baseDirectory);
    }

    private static bool HasRunValue(IStartupStore store)
    {
        try
        {
            return store.ReadRunValue(ValueName) is not null
                   || store.ReadRunValue(LegacyValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static void RemoveRunValues(IStartupStore store)
    {
        try
        {
            store.DeleteRunValue(ValueName);
            store.DeleteRunValue(LegacyValueName);
        }
        catch
        {
            /* not registered */
        }
    }

    private sealed class WindowsStartupStore : IStartupStore
    {
        private readonly TaskService? _tasks;

        public WindowsStartupStore(TaskService? tasks) => _tasks = tasks;

        public bool TaskExists(string name)
        {
            if (_tasks is null)
                return false;
            return _tasks.GetTask(name) is not null;
        }

        public void DeleteTask(string name)
        {
            if (_tasks is null)
                return;
            _tasks.RootFolder.DeleteTask(name, false);
        }

        public bool TryRegisterAutostartTask(string exePath)
        {
            if (_tasks is null)
                return false;

            try
            {
                var definition = _tasks.NewTask();
                definition.RegistrationInfo.Description =
                    "Aeroway — запуск в трее при входе в Windows.";
                definition.Settings.DisallowStartIfOnBatteries = false;
                definition.Settings.StopIfGoingOnBatteries = false;
                definition.Settings.ExecutionTimeLimit = TimeSpan.Zero;

                var trigger = new LogonTrigger
                {
                    UserId = WindowsIdentity.GetCurrent().Name
                };
                definition.Triggers.Add(trigger);

                var workDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
                definition.Actions.Add(new ExecAction(exePath, "--tray", workDir));

                definition.Principal.RunLevel = TaskRunLevel.Highest;
                definition.Principal.UserId = WindowsIdentity.GetCurrent().Name;
                definition.Principal.LogonType = TaskLogonType.InteractiveToken;

                _tasks.RootFolder.RegisterTaskDefinition(TaskName, definition);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string? ReadRunValue(string name)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(name) as string;
        }

        public void WriteRunValue(string name, string value)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
            key?.SetValue(name, value);
        }

        public void DeleteRunValue(string name)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.DeleteValue(name, false);
        }
    }
}

internal interface IStartupStore
{
    bool TaskExists(string name);
    void DeleteTask(string name);
    bool TryRegisterAutostartTask(string exePath);
    string? ReadRunValue(string name);
    void WriteRunValue(string name, string value);
    void DeleteRunValue(string name);
}
