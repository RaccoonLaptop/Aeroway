using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZapretUI.Helpers;

public static class UiHelpers
{
    public static void ShowError(string message, Window? owner = null) =>
        RunOnUiThread(() =>
        {
            var window = owner ?? GetActiveWindow();
            if (window is MainWindow main)
            {
                main.ShowToast(message, error: true);
                return;
            }

            MessageWindow.Show(message, Loc.T("app.title"), window);
        });

    public static void ShowInfo(string message, Window? owner = null) =>
        RunOnUiThread(() => MessageWindow.Show(message, Loc.T("app.title"), owner ?? GetActiveWindow()));

    public static void ShowResult(Window? owner, string title, string text) =>
        RunOnUiThread(() => ResultWindow.Show(owner, title, text));

    public static bool Confirm(string message, Window? owner = null) =>
        RunOnUiThread(() => ConfirmWindow.Show(message, owner ?? GetActiveWindow()));

    public static Task RunWithLoadingAsync(Window? owner, string message, Func<Task> action) =>
        RunWithLoadingAsync<object?>(owner, message, async () =>
        {
            await action().ConfigureAwait(true);
            return null;
        });

    public static Task<T> RunWithLoadingAsync<T>(Window? owner, string message, Func<Task<T>> action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
            return RunWithLoadingCoreAsync(owner, message, action);

        return dispatcher.InvokeAsync(() => RunWithLoadingCoreAsync(owner, message, action)).Task.Unwrap();
    }

    private static async Task<T> RunWithLoadingCoreAsync<T>(Window? owner, string message, Func<Task<T>> action)
    {
        var loading = new LoadingWindow(message, owner ?? GetActiveWindow());
        loading.Show();
        try
        {
            return await action().ConfigureAwait(true);
        }
        finally
        {
            loading.Close();
        }
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }

    private static T RunOnUiThread<T>(Func<T> func)
    {
        var dispatcher = Application.Current.Dispatcher;
        return dispatcher.CheckAccess() ? func() : dispatcher.Invoke(func);
    }

    private static Window? GetActiveWindow() =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current?.MainWindow;

    public static Button CreateNavButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            Style = Application.Current.FindResource("NavButton") as Style,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 2, 0, 2)
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    public static TextBlock CreateSectionHeader(string text) => new()
    {
        Text = text,
        FontSize = 13,
        FontWeight = FontWeights.SemiBold,
        Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
        Margin = new Thickness(0, 16, 0, 8)
    };

    public static void OpenFolder(string path)
    {
        var full = Path.GetFullPath(path);
        if (!Directory.Exists(full))
        {
            ShowError(Loc.F("common.folder_missing", full));
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = full,
            UseShellExecute = true
        });
    }
}
