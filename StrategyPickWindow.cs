using System.ComponentModel;
using System.Windows.Input;
using Aeroway.Helpers;
using Aeroway.Models;
using Aeroway.Services;

namespace Aeroway;

/// <summary>
/// Модальное окно подбора стратегии. Доступно только в сборке DEV.
/// </summary>
public sealed class StrategyPickWindow : Window
{
    private readonly StrategyPickSession _session;
    private readonly TextBlock _statusText;
    private readonly TextBlock _progressText;
    private readonly TextBlock _baselineText;
    private readonly ProgressBar _progressBar;
    private readonly TextBlock _currentText;
    private readonly TextBlock _summaryText;
    private readonly StackPanel _targetsPanel;
    private readonly StackPanel _queuePanel;
    private readonly ScrollViewer _queueScroll;
    private readonly Border _chipHost;
    private readonly TextBlock _reasonText;
    private readonly TextBlock _verdictHint;
    private readonly Button _stopBtn;
    private readonly Button _applyBtn;
    private bool _allowClose;
    private bool _closing;
    private bool _closed;
    private bool _applied;
    private bool _restoreAsked;
    private bool _applying;
    private string? _scrolledFile;
    private Border? _runningRow;

    private StrategyPickWindow(ZapretPaths paths, StrategyService strategy, AppSettings settings)
    {
        Title = Loc.T("pick.title");
        Width = 1080;
        Height = 720;
        MinWidth = 960;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = BrushOf("BgBrush");
        Foreground = BrushOf("TextBrush");
        ResizeMode = ResizeMode.CanResize;

        _session = new StrategyPickSession(paths, strategy, settings, action =>
        {
            if (_closed || Dispatcher.HasShutdownStarted)
                return;
            Dispatcher.InvokeAsync(action);
        });

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock
        {
            Text = Loc.T("pick.title"),
            FontSize = 22,
            FontWeight = FontWeights.Bold
        });
        header.Children.Add(new TextBlock
        {
            Text = Loc.T("pick.subtitle"),
            Foreground = BrushOf("TextMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var progressCard = Card();
        progressCard.Margin = new Thickness(0, 0, 0, 12);
        var progressStack = new StackPanel();
        var progressHeader = new Grid();
        progressHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        progressHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _statusText = new TextBlock
        {
            Foreground = BrushOf("TextMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 12, 0)
        };
        _progressText = new TextBlock
        {
            Foreground = BrushOf("AccentBrush"),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_progressText, 1);
        progressHeader.Children.Add(_statusText);
        progressHeader.Children.Add(_progressText);
        progressStack.Children.Add(progressHeader);
        _baselineText = new TextBlock
        {
            Foreground = BrushOf("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        progressStack.Children.Add(_baselineText);
        _progressBar = new ProgressBar
        {
            Height = 6,
            Minimum = 0,
            Maximum = 1,
            Margin = new Thickness(0, 8, 0, 0),
            Background = BrushOf("InputBrush"),
            Foreground = BrushOf("AccentBrush"),
            BorderThickness = new Thickness(0)
        };
        progressStack.Children.Add(_progressBar);
        progressCard.Child = progressStack;
        Grid.SetRow(progressCard, 1);
        root.Children.Add(progressCard);

        var split = new Grid();
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = Card();
        var leftGrid = new Grid();
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        leftGrid.Children.Add(SectionTitle(Loc.T("pick.targets")));
        _currentText = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushOf("AccentBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 4)
        };
        Grid.SetRow(_currentText, 1);
        leftGrid.Children.Add(_currentText);
        _summaryText = new TextBlock
        {
            Foreground = BrushOf("TextMutedBrush"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(_summaryText, 2);
        leftGrid.Children.Add(_summaryText);
        var targetsScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        _targetsPanel = new StackPanel();
        targetsScroll.Content = _targetsPanel;
        MouseWheelScrollHelper.Attach(targetsScroll);
        Grid.SetRow(targetsScroll, 3);
        leftGrid.Children.Add(targetsScroll);
        left.Child = leftGrid;
        split.Children.Add(left);

        var right = Card();
        Grid.SetColumn(right, 2);
        var rightGrid = new Grid();
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rightGrid.Children.Add(SectionTitle(Loc.T("pick.queue")));
        var queueHint = new TextBlock
        {
            Text = Loc.T("pick.queue_hint"),
            Foreground = BrushOf("TextMutedBrush"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(queueHint, 1);
        rightGrid.Children.Add(queueHint);
        _queueScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        _queuePanel = new StackPanel();
        _queueScroll.Content = _queuePanel;
        MouseWheelScrollHelper.Attach(_queueScroll);
        Grid.SetRow(_queueScroll, 2);
        rightGrid.Children.Add(_queueScroll);
        right.Child = rightGrid;
        split.Children.Add(right);
        Grid.SetRow(split, 3);
        root.Children.Add(split);

        var footer = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var verdict = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _chipHost = new Border { VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
        _reasonText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360
        };
        _verdictHint = new TextBlock
        {
            Text = Loc.T("pick.verdict_waiting"),
            Foreground = BrushOf("TextMutedBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        verdict.Children.Add(_chipHost);
        verdict.Children.Add(_reasonText);
        verdict.Children.Add(_verdictHint);
        footer.Children.Add(verdict);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _stopBtn = new Button
        {
            Content = Loc.T("pick.stop"),
            Style = (Style)FindResource("DangerButton"),
            MinWidth = 120,
            Margin = new Thickness(0, 0, 8, 0)
        };
        _stopBtn.Click += async (_, _) => await _session.StopAsync();
        _applyBtn = new Button
        {
            Content = Loc.T("pick.apply"),
            Style = (Style)FindResource("PrimaryButton"),
            MinWidth = 180,
            Margin = new Thickness(0, 0, 8, 0)
        };
        _applyBtn.Click += async (_, _) => await ApplySelectedAsync();
        var closeBtn = new Button
        {
            Content = Loc.T("common.close"),
            Style = (Style)FindResource("SecondaryButton"),
            MinWidth = 110
        };
        closeBtn.Click += (_, _) => Close();
        buttons.Children.Add(_stopBtn);
        buttons.Children.Add(_applyBtn);
        buttons.Children.Add(closeBtn);
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;
        _session.Changed += Refresh;
        Closing += OnClosing;
        Closed += async (_, _) =>
        {
            _closed = true;
            await _session.DisposeAsync();
        };
        Loaded += async (_, _) =>
        {
            try
            {
                await _session.StartAsync();
            }
            catch (Exception ex)
            {
                UiHelpers.ShowError(ex.Message, this);
            }
        };
    }

    public static bool TryShow(Window? owner, ZapretPaths paths, StrategyService strategy, AppSettings settings)
    {
        if (!AppBuild.IsDevBuild)
            return false;

        var window = new StrategyPickWindow(paths, strategy, settings);
        if (owner is not null)
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        window.ShowDialog();
        return true;
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;

        e.Cancel = true;
        if (_closing)
            return;

        _closing = true;
        try
        {
            if (_session.Board.Phase is PickPhase.Baseline or PickPhase.Testing)
            {
                if (!UiHelpers.Confirm(Loc.T("pick.close_running"), this))
                    return;

                await _session.StopAsync();
                return;
            }

            if (!_applied && !_restoreAsked && _session.PreviousStrategy is { Length: > 0 } previous)
            {
                _restoreAsked = true;
                var name = StrategyDisplayHelper.ToDisplayName(previous);
                if (UiHelpers.Confirm(Loc.F("pick.restore_ask", name), this))
                {
                    try
                    {
                        await _session.ApplyAsync(previous);
                    }
                    catch (Exception ex)
                    {
                        _restoreAsked = false;
                        UiHelpers.ShowError(ex.Message, this);
                        return;
                    }
                }
            }

            _allowClose = true;
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
        finally
        {
            _closing = false;
        }
    }

    private async Task ApplySelectedAsync()
    {
        var file = SelectedFile();
        if (file is null || _applying)
            return;
        if (_session.Board.Phase is PickPhase.Baseline or PickPhase.Testing)
            return;

        _applying = true;
        Refresh();
        try
        {
            await _session.ApplyAsync(file);
            _applied = true;
            var name = StrategyDisplayHelper.ToDisplayName(file);
            _session.Board.StatusText = Loc.F("tools.test_applied", name);
            UiHelpers.ShowInfo(Loc.F("tools.test_applied", name), this);
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(ex.Message, this);
        }
        finally
        {
            _applying = false;
            Refresh();
        }
    }

    private string? SelectedFile()
    {
        var board = _session.Board;
        if (board.Find(board.ReviewFile) is { } review)
            return review.FileName;
        return board.Best()?.FileName;
    }

    private void Refresh()
    {
        if (_closed)
            return;

        var board = _session.Board;
        _statusText.Text = board.StatusText;
        _progressText.Text = board.ProgressText;
        _progressBar.IsIndeterminate = board.Phase == PickPhase.Baseline;
        _progressBar.Value = board.Progress;
        _baselineText.Text = board.HasBaseline
            ? Loc.F("pick.baseline_caption", board.BaselineOpen, board.BaselineClosed)
            : board.Phase is PickPhase.Testing or PickPhase.Finished or PickPhase.Stopped
                ? Loc.T("pick.baseline_unavailable")
                : "";

        ResolveDisplay(board, out var title, out var targets, out var compare, out var doneAssessment);
        _currentText.Text = title;

        StrategyPickAssessment? deltas = doneAssessment;
        if (deltas is null && compare && targets.Count > 0)
            deltas = StrategyPickVerdict.Assess(board.Baseline, targets, null);

        var measured = deltas is not null
            && (deltas.UsedBaseline
                || deltas.UnblockedCount + deltas.AlreadyOpenCount + deltas.StillClosedCount + deltas.RegressedCount > 0);
        _summaryText.Text = measured
            ? Loc.F("pick.summary",
                deltas!.UnblockedCount,
                deltas.AlreadyOpenCount,
                deltas.StillClosedCount + deltas.RegressedCount)
            : compare ? Loc.T("pick.legend") : "";

        _targetsPanel.Children.Clear();
        if (targets.Count == 0)
        {
            _targetsPanel.Children.Add(Muted(Loc.T("pick.targets_empty")));
        }
        else
        {
            var width = TestTargetRowFormatter.ComputeNameWidth(targets);
            foreach (var row in targets)
                _targetsPanel.Children.Add(BuildTargetLine(row, width, deltas));
        }

        var offset = _queueScroll.VerticalOffset;
        _queuePanel.Children.Clear();
        _runningRow = null;
        if (board.Candidates.Count == 0)
        {
            _queuePanel.Children.Add(Muted(Loc.T("pick.queue_empty")));
        }
        else
        {
            foreach (var candidate in board.Candidates)
                _queuePanel.Children.Add(BuildQueueRow(board, candidate));
        }

        _queueScroll.ScrollToVerticalOffset(offset);
        if (board.Phase == PickPhase.Testing
            && board.RunningFile is { } running
            && !running.Equals(_scrolledFile, StringComparison.OrdinalIgnoreCase)
            && _runningRow is not null)
        {
            _scrolledFile = running;
            _runningRow.BringIntoView();
        }

        var showChip = doneAssessment is { Verdict: not PickVerdict.None };
        _chipHost.Visibility = showChip ? Visibility.Visible : Visibility.Collapsed;
        _chipHost.Child = showChip ? BuildChip(doneAssessment!) : null;
        _reasonText.Text = showChip ? ReasonText(doneAssessment!.Reason) : "";
        _reasonText.Foreground = showChip ? ChipForeground(doneAssessment!) : BrushOf("TextMutedBrush");
        _verdictHint.Visibility = showChip ? Visibility.Collapsed : Visibility.Visible;

        var runningPhase = board.Phase is PickPhase.Baseline or PickPhase.Testing;
        _stopBtn.Visibility = runningPhase ? Visibility.Visible : Visibility.Collapsed;
        _stopBtn.IsEnabled = runningPhase;
        _applyBtn.IsEnabled = !runningPhase && !_applying && SelectedFile() is not null;
    }

    private static void ResolveDisplay(
        StrategyPickBoard board,
        out string title,
        out IReadOnlyList<TestTargetRow> targets,
        out bool compare,
        out StrategyPickAssessment? doneAssessment)
    {
        if (board.Phase == PickPhase.Baseline)
        {
            title = Loc.T("pick.current_baseline");
            targets = board.Baseline;
            compare = false;
            doneAssessment = null;
            return;
        }

        if (board.Phase == PickPhase.Testing)
        {
            var running = board.Find(board.RunningFile);
            if (running is { Targets.Count: > 0 })
            {
                title = Loc.F("pick.current", running.DisplayName);
                targets = running.Targets;
                compare = board.HasBaseline;
                doneAssessment = null;
                return;
            }

            title = Loc.T("pick.current_waiting");
            targets = board.Baseline;
            compare = false;
            doneAssessment = null;
            return;
        }

        var review = board.Find(board.ReviewFile);
        if (review is not null)
        {
            title = Loc.F("pick.review", review.DisplayName);
            targets = review.Targets;
            compare = board.HasBaseline && review.Targets.Count > 0;
            doneAssessment = review.Assessment;
            return;
        }

        title = Loc.T("pick.current_waiting");
        targets = [];
        compare = false;
        doneAssessment = null;
    }

    private UIElement BuildTargetLine(TestTargetRow row, int nameWidth, StrategyPickAssessment? deltas)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        TestTargetRowFormatter.ApplyRowInlines(text, row, nameWidth);
        grid.Children.Add(text);

        if (deltas is not null && !row.PingOnly)
        {
            var kind = deltas.DeltaFor(row.Name);
            var tag = TagText(kind);
            if (tag.Length > 0)
            {
                var chip = new TextBlock
                {
                    Text = tag,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(12, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = TagBrush(kind)
                };
                Grid.SetColumn(chip, 1);
                grid.Children.Add(chip);
            }
        }

        return grid;
    }

    private Border BuildQueueRow(StrategyPickBoard board, StrategyPickCandidate candidate)
    {
        var selected = board.Phase is not (PickPhase.Baseline or PickPhase.Testing)
            && board.ReviewFile is not null
            && candidate.FileName.Equals(board.ReviewFile, StringComparison.OrdinalIgnoreCase);
        var running = candidate.State == PickQueueState.Running;
        var card = new Border
        {
            Background = BrushOf(selected || running ? "PanelOverlayBrush" : "InputBrush"),
            BorderBrush = BrushOf(selected || running ? "AccentBrush" : "BorderBrush"),
            BorderThickness = new Thickness(selected || running ? 2 : 1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 8),
            Opacity = candidate.State == PickQueueState.Pending ? 0.72 : 1,
            Cursor = board.Phase is PickPhase.Baseline or PickPhase.Testing ? Cursors.Arrow : Cursors.Hand
        };
        if (running)
            _runningRow = card;

        card.MouseLeftButtonUp += (_, _) =>
        {
            if (_session.Board.TrySelect(candidate.FileName))
                Refresh();
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var glyph = new TextBlock
        {
            Text = string.IsNullOrEmpty(candidate.Glyph) ? "·" : candidate.Glyph,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = GlyphBrush(candidate.Glyph),
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(glyph);

        var text = new StackPanel { Margin = new Thickness(4, 0, 8, 0) };
        text.Children.Add(new TextBlock
        {
            Text = candidate.DisplayName,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        var state = StateText(candidate.State);
        if (candidate.State == PickQueueState.Done && candidate.Assessment is { Reason: not PickReason.None } assessment)
            state += " · " + ReasonText(assessment.Reason);
        text.Children.Add(new TextBlock
        {
            Text = state,
            FontSize = 11,
            Foreground = BrushOf(running ? "AccentBrush" : "TextMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        if (candidate.State == PickQueueState.Done && candidate.Assessment is { Verdict: not PickVerdict.None } chipAssessment)
        {
            var chip = BuildChip(chipAssessment);
            chip.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(chip, 2);
            grid.Children.Add(chip);
        }

        card.Child = grid;
        return card;
    }

    private static Border BuildChip(StrategyPickAssessment assessment)
    {
        var chip = new Border
        {
            Background = ChipBackground(assessment),
            BorderBrush = ChipForeground(assessment),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 3, 8, 3)
        };
        chip.Child = new TextBlock
        {
            Text = ChipText(assessment.Verdict),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = ChipForeground(assessment)
        };
        return chip;
    }

    private static string StateText(PickQueueState state) => state switch
    {
        PickQueueState.Pending => Loc.T("pick.pending"),
        PickQueueState.Running => Loc.T("pick.running"),
        PickQueueState.Done => Loc.T("pick.done"),
        _ => ""
    };

    private static string ChipText(PickVerdict verdict) => verdict switch
    {
        PickVerdict.Works => Loc.T("pick.chip_works"),
        PickVerdict.Partial => Loc.T("pick.chip_partial"),
        PickVerdict.Failed => Loc.T("pick.chip_failed"),
        _ => ""
    };

    private static string ReasonText(PickReason reason) => reason switch
    {
        PickReason.UnblockedByBypass => Loc.T("pick.reason_unblocked"),
        PickReason.AlreadyOpen => Loc.T("pick.reason_already"),
        PickReason.Mixed => Loc.T("pick.reason_mixed"),
        PickReason.NoImprovement => Loc.T("pick.reason_none"),
        PickReason.ScoreOnly => Loc.T("pick.reason_score"),
        PickReason.Skipped => Loc.T("pick.reason_skipped"),
        _ => ""
    };

    private static string TagText(TargetDeltaKind kind) => kind switch
    {
        TargetDeltaKind.Unblocked => Loc.T("pick.tag_unblocked"),
        TargetDeltaKind.AlreadyOpen => Loc.T("pick.tag_already"),
        TargetDeltaKind.StillClosed => Loc.T("pick.tag_closed"),
        TargetDeltaKind.Regressed => Loc.T("pick.tag_regressed"),
        _ => ""
    };

    private static Brush GlyphBrush(string glyph) => glyph switch
    {
        "✓" => BrushOf("SuccessBrush"),
        "≈" => BrushOf("WarningBrush"),
        "✗" => BrushOf("ErrorBrush"),
        _ => BrushOf("TextMutedBrush")
    };

    private static Brush TagBrush(TargetDeltaKind kind) => kind switch
    {
        TargetDeltaKind.Unblocked => BrushOf("SuccessBrush"),
        TargetDeltaKind.AlreadyOpen => BrushOf("AccentBrush"),
        TargetDeltaKind.Regressed => BrushOf("ErrorBrush"),
        TargetDeltaKind.StillClosed => BrushOf("ErrorBrush"),
        _ => BrushOf("TextMutedBrush")
    };

    private static Brush ChipForeground(StrategyPickAssessment assessment)
    {
        if (assessment.Verdict == PickVerdict.Works && assessment.Reason == PickReason.AlreadyOpen)
            return BrushOf("AccentBrush");
        return assessment.Verdict switch
        {
            PickVerdict.Works => BrushOf("SuccessBrush"),
            PickVerdict.Partial => BrushOf("WarningBrush"),
            PickVerdict.Failed => BrushOf("ErrorBrush"),
            _ => BrushOf("TextBrush")
        };
    }

    private static Brush ChipBackground(StrategyPickAssessment assessment)
    {
        if (assessment.Verdict == PickVerdict.Works && assessment.Reason == PickReason.AlreadyOpen)
            return BrushOf("SurfaceBrush");
        return assessment.Verdict switch
        {
            PickVerdict.Works => BrushOf("SuccessFillBrush"),
            PickVerdict.Partial => BrushOf("BusyFillBrush"),
            _ => BrushOf("InputBrush")
        };
    }

    private static Border Card() => new()
    {
        Background = BrushOf("PanelOverlayBrush"),
        BorderBrush = BrushOf("BorderBrush"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(14)
    };

    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = BrushOf("TextMutedBrush")
    };

    private static TextBlock Muted(string text) => new()
    {
        Text = text,
        Foreground = BrushOf("TextMutedBrush"),
        TextWrapping = TextWrapping.Wrap,
        FontSize = 12
    };

    private static Brush BrushOf(string key) => (Brush)Application.Current.FindResource(key);
}
