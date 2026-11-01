using System.Text;
using System.Text.RegularExpressions;
using Aeroway.Models;
using Aeroway.Services;

namespace Aeroway.Helpers;

public sealed class TestScriptAutoResponder
{
    private readonly StringBuilder _buffer = new();
    private readonly string _testTypeChoice;
    private readonly PresetTestScope _scope;
    private readonly IReadOnlyList<string> _batFiles;
    private readonly EmbeddedTerminalHost _terminal;
    private readonly SemaphoreSlim _inputLock = new(1, 1);
    private bool _testTypeAnswered;
    private bool _modeAnswered;
    private bool _configSelectionAnswered;
    private bool _closePromptAnswered;

    public TestScriptAutoResponder(
        PresetTestKind kind,
        PresetTestScope scope,
        IReadOnlyList<string> batFiles,
        EmbeddedTerminalHost terminal)
    {
        _testTypeChoice = kind == PresetTestKind.DpiFreeze ? "2" : "1";
        _scope = scope;
        _batFiles = batFiles;
        _terminal = terminal;
    }

    public void Reset()
    {
        _buffer.Clear();
        _testTypeAnswered = false;
        _modeAnswered = false;
        _configSelectionAnswered = false;
        _closePromptAnswered = false;
    }

    public async Task FeedAsync(string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;

        await _inputLock.WaitAsync();
        try
        {
            _buffer.Append(StripAnsi(chunk));
            if (_buffer.Length > 8000)
                _buffer.Remove(0, _buffer.Length - 4000);

            var text = _buffer.ToString();

            if (!_configSelectionAnswered && ContainsSelectionPrompt(text))
            {
                _configSelectionAnswered = true;
                await _terminal.WriteInputAsync(ResolveSelectionAnswer());
                _buffer.Clear();
                return;
            }

            if (!_testTypeAnswered
                && text.Contains("Select test type:", StringComparison.OrdinalIgnoreCase)
                && ContainsEnterOneOrTwo(text))
            {
                _testTypeAnswered = true;
                await _terminal.WriteInputAsync(_testTypeChoice);
                _buffer.Clear();
                return;
            }

            if (!_modeAnswered
                && text.Contains("Select test run mode:", StringComparison.OrdinalIgnoreCase)
                && ContainsEnterOneOrTwo(text))
            {
                _modeAnswered = true;
                await _terminal.WriteInputAsync(_scope.TestAll ? "1" : "2");
                _buffer.Clear();
                return;
            }

            if (!_closePromptAnswered
                && (text.Contains("Press any key", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Нажмите любую клавишу", StringComparison.OrdinalIgnoreCase)))
            {
                _closePromptAnswered = true;
                _buffer.Clear();
                _ = RespondClosePromptAsync();
            }
        }
        finally
        {
            _inputLock.Release();
        }
    }

    private async Task RespondClosePromptAsync()
    {
        // Flowseal 1.10.3 drains leftover keys before waiting, so send after a short delay.
        await Task.Delay(400);
        await _terminal.SendKeyAsync(' ');
    }

    private static bool ContainsEnterOneOrTwo(string text) =>
        text.Contains("Enter 1 or 2", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Введите 1 или 2", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsSelectionPrompt(string text) =>
        text.Contains("Enter numbers", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Введите номера", StringComparison.OrdinalIgnoreCase);

    private string ResolveSelectionAnswer()
    {
        if (_scope.TestAll || _scope.SelectedStrategyFiles.Count == 0)
            return "0";

        var indices = new List<int>();
        foreach (var file in _scope.SelectedStrategyFiles)
        {
            for (var i = 0; i < _batFiles.Count; i++)
            {
                if (_batFiles[i].Equals(file, StringComparison.OrdinalIgnoreCase))
                    indices.Add(i + 1);
            }
        }

        if (indices.Count == 0)
            return "1";

        indices.Sort();
        return string.Join(",", indices.Distinct());
    }

    private static string StripAnsi(string text) =>
        Regex.Replace(text, @"\x1B(?:\][^\x07]*\x07|\[[\d;?]*[ -/]*[@-~])", "");
}
