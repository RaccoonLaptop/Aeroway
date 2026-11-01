using Aeroway.Helpers;
using Aeroway.Models;

namespace Aeroway.Services;

/// <summary>
/// Подбор стратегии: базовая проверка без обхода, затем стандартный прогон test zapret.ps1.
/// </summary>
public sealed class StrategyPickSession : IAsyncDisposable
{
    private readonly ZapretPaths _paths;
    private readonly StrategyService _strategy;
    private readonly AppSettings _settings;
    private readonly Action<Action> _marshal;
    private readonly EmbeddedTerminalHost _terminal = new();
    private readonly TestRunTracker _tracker = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Action<string> _onOutput;
    private readonly Action<int> _onExit;
    private readonly Action<string> _onError;
    private TestScriptAutoResponder? _auto;
    private int _completed;
    private int _disposed;
    private bool _started;

    public StrategyPickSession(
        ZapretPaths paths,
        StrategyService strategy,
        AppSettings settings,
        Action<Action> marshal)
    {
        _paths = paths;
        _strategy = strategy;
        _settings = settings;
        _marshal = marshal;
        _onOutput = chunk => _marshal(() => { _ = OnOutputAsync(chunk); });
        _onExit = code => _marshal(() => OnExited(code));
        _onError = message => _marshal(() =>
        {
            Board.StatusText = message;
            Changed?.Invoke();
        });
        _terminal.OutputReceived += _onOutput;
        _terminal.ProcessExited += _onExit;
        _terminal.ErrorOccurred += _onError;
        _tracker.Changed += OnTrackerChanged;
    }

    public StrategyPickBoard Board { get; } = new();

    public string? PreviousStrategy { get; private set; }

    public event Action? Changed;

    public async Task StartAsync()
    {
        if (!AppBuild.IsDevBuild || _started)
            return;

        _started = true;
        var files = _paths.GetStrategyFiles().ToList();
        Board.Seed(StrategyDisplayHelper.LoadItems(_paths.Root, files));
        if (files.Count == 0)
        {
            Board.Phase = PickPhase.Finished;
            Board.StatusText = Loc.T("pick.status_no_strategies");
            Changed?.Invoke();
            return;
        }

        var script = ProcessRunner.ResolveTestScript(_paths.Root);
        if (script is null)
        {
            Board.Phase = PickPhase.Finished;
            Board.StatusText = Loc.T("pick.status_no_script");
            Changed?.Invoke();
            return;
        }

        Board.Phase = PickPhase.Baseline;
        Board.StatusText = Loc.T("pick.status_baseline");
        Changed?.Invoke();

        try
        {
            if (_strategy.IsRunning())
            {
                PreviousStrategy = string.IsNullOrWhiteSpace(_settings.LastStrategy)
                    ? null
                    : _settings.LastStrategy;
                await _strategy.StopStrategyAsync(_cts.Token);
            }

            if (StopIfCancelled())
                return;

            TestTargetsService.EnsureTargetsFile(_paths);
            var baseline = await BaselineTargetProbe.ProbeAsync(
                TestTargetsLoader.LoadEndpoints(_paths),
                _cts.Token);
            if (StopIfCancelled())
                return;

            Board.SetBaseline(baseline);
            Board.Phase = PickPhase.Testing;
            Board.StatusText = Loc.T("pick.status_starting");
            Changed?.Invoke();

            _auto = new TestScriptAutoResponder(
                PresetTestKind.Standard,
                new PresetTestScope { TestAll = true },
                files,
                _terminal);
            _tracker.BeginRun(PresetTestKind.Standard, TestTargetsLoader.LoadDefinitions(_paths));
            await _terminal.StartPowerShellScriptAsync(script, _paths.Root, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            Complete(stopped: true, exitCode: -1);
        }
        catch (Exception ex)
        {
            if (_tracker.IsRunning)
                _tracker.StopRun();
            Complete(stopped: true, exitCode: -1);
            Board.StatusText = ex.Message;
            Changed?.Invoke();
        }
    }

    public async Task StopAsync()
    {
        if (Board.Phase is not (PickPhase.Baseline or PickPhase.Testing))
            return;

        _cts.Cancel();
        try
        {
            await _terminal.StopAsync();
        }
        catch
        {
            // Окно всё равно остаётся на уже собранном результате.
        }

        if (_tracker.IsRunning)
            _tracker.StopRun();
        Complete(stopped: true, exitCode: -1);
    }

    public async Task ApplyAsync(string fileName)
    {
        _settings.LastStrategy = fileName;
        _settings.Save();
        if (_strategy.IsRunning())
            await _strategy.StopStrategyAsync();
        await _strategy.StartStrategyAsync(fileName);
        if (Application.Current?.MainWindow is MainWindow main)
            main.SyncHomeStrategy(fileName);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _cts.Cancel();
        _terminal.OutputReceived -= _onOutput;
        _terminal.ProcessExited -= _onExit;
        _terminal.ErrorOccurred -= _onError;
        _tracker.Changed -= OnTrackerChanged;
        try
        {
            await _terminal.DisposeAsync();
        }
        catch
        {
            // Закрытие окна не должно ронять процесс.
        }

        _cts.Dispose();
    }

    private bool StopIfCancelled()
    {
        if (!_cts.IsCancellationRequested)
            return false;

        Complete(stopped: true, exitCode: -1);
        return true;
    }

    private async Task OnOutputAsync(string chunk)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        try
        {
            _tracker.Feed(chunk);
            if (_auto is not null)
                await _auto.FeedAsync(chunk);
        }
        catch (Exception ex)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;
            Board.StatusText = ex.Message;
            Changed?.Invoke();
        }
    }

    private void OnExited(int exitCode)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        _tracker.EndRun(exitCode);
        Complete(stopped: false, exitCode);
    }

    private void OnTrackerChanged()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        if (Board.Phase is PickPhase.Finished or PickPhase.Stopped)
            return;

        var completed = new List<StrategyPickCompletion>();
        foreach (var score in _tracker.Scores)
        {
            if (!_tracker.TryGetSnapshot(score.FileName, out var snapshot))
                continue;

            completed.Add(new StrategyPickCompletion
            {
                FileName = score.FileName,
                Targets = snapshot.Targets,
                ScoreGlyph = score.Glyph,
                RankScore = score.RankScore
            });
        }

        Board.SyncLive(_tracker.CurrentPreset, _tracker.Targets.ToList(), completed);
        if (Board.Phase == PickPhase.Testing && _tracker.IsRunning)
        {
            Board.Progress = _tracker.Progress;
            Board.ProgressText = _tracker.ProgressText;
            if (!string.IsNullOrWhiteSpace(_tracker.StatusText))
                Board.StatusText = _tracker.StatusText;
        }

        Changed?.Invoke();
    }

    private void Complete(bool stopped, int exitCode)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            return;

        Board.Finish(stopped);
        if (!stopped)
            Board.Progress = 1;

        Board.StatusText = stopped
            ? Loc.T("pick.status_stopped")
            : exitCode == 0
                ? Loc.T("pick.status_done")
                : Loc.F("tools.test_exited", exitCode) + " " + Loc.T("pick.status_done");

        Board.ReviewFile ??= Board.Best()?.FileName
            ?? Board.Candidates.FirstOrDefault(candidate => candidate.State == PickQueueState.Done)?.FileName;
        Changed?.Invoke();
    }
}
