using Microsoft.Win32;

namespace Aeroway.Services;

/// <summary>Перезапускает обход после выхода из сна, если он работал до suspend.</summary>
public sealed class PowerResumeService : IDisposable
{
    private readonly Func<bool> _isBypassRunning;
    private readonly Func<Task> _restartBypassAsync;
    private bool _wasRunningBeforeSuspend;

    public PowerResumeService(Func<bool> isBypassRunning, Func<Task> restartBypassAsync)
    {
        _isBypassRunning = isBypassRunning;
        _restartBypassAsync = restartBypassAsync;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                _wasRunningBeforeSuspend = _isBypassRunning();
                break;
            case PowerModes.Resume:
                if (_wasRunningBeforeSuspend && !_isBypassRunning())
                    _ = SafeRestartAsync();
                _wasRunningBeforeSuspend = false;
                break;
        }
    }

    private async Task SafeRestartAsync()
    {
        try
        {
            await _restartBypassAsync().ConfigureAwait(false);
        }
        catch
        {
            /* logged by caller */
        }
    }

    public void Dispose() => SystemEvents.PowerModeChanged -= OnPowerModeChanged;
}
