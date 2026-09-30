using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ICSharpCode.AvalonEdit;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZapretUI;
using ZapretUI.Helpers;
using ZapretUI.Services;

namespace ZapretUI.Pages;

public partial class ServicePage : UserControl
{
    private const string FlowsealUrl = "https://github.com/Flowseal/zapret-discord-youtube";
    private const string AppUrl = "https://github.com/RaccoonLaptop/Aeroway";
    private const string TelegramUrl = "https://t.me/AerowayUI";
    private const string DonateUrl = "https://raccoonlaptop.github.io/Aeroway/donate.html";

    private readonly ZapretPaths _paths;
    private readonly AppSettings _settings;
    private readonly ServiceSettingsService _settingsSvc;
    private readonly UpdateService _updates;
    private readonly HostsCatalogService _hostsCatalog;
    private bool _hostsGuard;
    private TextBlock _gameFilterStatus = null!;
    private TextBlock _ipsetStatus = null!;
    private Button _gameDisabledBtn = null!;
    private Button _gameTcpUdpBtn = null!;
    private Button _gameTcpBtn = null!;
    private Button _gameUdpBtn = null!;
    private TextBox _gameTcpPorts = null!;
    private TextBox _gameUdpPorts = null!;
    private bool _updatingPortBoxes;
    private TextBlock _gameFilterPortsApplied = null!;
    private TextBlock _gameFilterPortsFeedback = null!;
    private Button _gameApplyPortsBtn = null!;
    private DispatcherTimer? _portsFeedbackTimer;
    private Button _ipsetLoadedBtn = null!;
    private Button _ipsetNoneBtn = null!;
    private Button _ipsetAnyBtn = null!;
    private TextBlock _componentStatus = null!;

    public ServicePage(ZapretPaths paths, StrategyService strategy, AppSettings settings)
    {
        _paths = paths;
        _ = strategy;
        _settings = settings;
        _settingsSvc = new ServiceSettingsService(paths);
        _updates = new UpdateService(paths);
        _hostsCatalog = new HostsCatalogService(_updates);
        BuildUi();
        RefreshStatuses();
        Unloaded += (_, _) => _portsFeedbackTimer?.Stop();
    }

    private Window? OwnerWindow => Window.GetWindow(this);

    private void BuildUi()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root = new StackPanel();

        root.Children.Add(new TextBlock { Text = Loc.T("service.title"), FontSize = 28, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        root.Children.Add(new TextBlock
        {
            Text = Loc.T("service.subtitle"),
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 20)
        });

        // Settings
        root.Children.Add(Section(Loc.T("service.section_settings")));
        var setCard = Card();
        var setStack = new StackPanel();

        _gameFilterStatus = StatusLine(Loc.T("service.game_filter"));
        setStack.Children.Add(_gameFilterStatus);
        _gameFilterPortsApplied = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        setStack.Children.Add(_gameFilterPortsApplied);
        var gameBtns = new WrapPanel { Margin = new Thickness(0, 4, 0, 12) };
        _gameDisabledBtn = SettingsBtn(Loc.T("service.disable"), () => ApplyGameFilterMode("disabled"));
        _gameTcpUdpBtn = SettingsBtn(Loc.T("service.tcp_udp"), () => ApplyGameFilterMode("all"));
        _gameTcpBtn = SettingsBtn(Loc.T("service.tcp_only"), () => ApplyGameFilterMode("tcp"));
        _gameUdpBtn = SettingsBtn(Loc.T("service.udp_only"), () => ApplyGameFilterMode("udp"));
        gameBtns.Children.Add(_gameDisabledBtn);
        gameBtns.Children.Add(_gameTcpUdpBtn);
        gameBtns.Children.Add(_gameTcpBtn);
        gameBtns.Children.Add(_gameUdpBtn);
        setStack.Children.Add(gameBtns);

        setStack.Children.Add(new TextBlock
        {
            Text = Loc.T("service.game_filter_ports_hint"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8)
        });
        _gameTcpPorts = PortRangeBox();
        _gameUdpPorts = PortRangeBox();
        setStack.Children.Add(PortRangeRow(Loc.T("service.game_filter_tcp_ports"), _gameTcpPorts));
        setStack.Children.Add(PortRangeRow(Loc.T("service.game_filter_udp_ports"), _gameUdpPorts));
        _gameApplyPortsBtn = ActionBtn(Loc.T("service.game_filter_apply_ports"), ApplyGameFilterPorts);
        _gameFilterPortsFeedback = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 6, 0, 0),
            Visibility = Visibility.Collapsed
        };
        var applyPorts = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        applyPorts.Children.Add(_gameApplyPortsBtn);
        applyPorts.Children.Add(_gameFilterPortsFeedback);
        setStack.Children.Add(applyPorts);

        _ipsetStatus = StatusLine(Loc.T("service.ipset_filter"));
        setStack.Children.Add(_ipsetStatus);
        var ipsetBtns = new WrapPanel { Margin = new Thickness(0, 4, 0, 12) };
        _ipsetLoadedBtn = SettingsBtn(Loc.T("service.ipset_loaded"), () => SetIpsetMode("loaded"));
        _ipsetNoneBtn = SettingsBtn(Loc.T("service.ipset_none"), () => SetIpsetMode("none"));
        _ipsetAnyBtn = SettingsBtn(Loc.T("service.ipset_any"), () => SetIpsetMode("any"));
        ipsetBtns.Children.Add(_ipsetLoadedBtn);
        ipsetBtns.Children.Add(_ipsetNoneBtn);
        ipsetBtns.Children.Add(_ipsetAnyBtn);
        setStack.Children.Add(ipsetBtns);

        var dataBtns = new WrapPanel { Margin = new Thickness(0, 0, 0, 0) };
        dataBtns.Children.Add(ActionBtn(Loc.T("service.update_ipset"), async () => await UpdateIpsetWithDialog()));
        dataBtns.Children.Add(ActionBtn(Loc.T("service.update_hosts"), async () => await UpdateHostsWithDialog()));
        dataBtns.Children.Add(ActionBtn(Loc.T("service.replace_fakes"), async () => await ReplaceFakesAsync()));
        setStack.Children.Add(dataBtns);

        setCard.Child = setStack;
        root.Children.Add(setCard);

        root.Children.Add(Section(Loc.T("service.section_hosts")));
        var hostsCard = Card();
        var hostsStack = new StackPanel();
        hostsStack.Children.Add(new TextBlock
        {
            Text = Loc.T("service.hosts_desc"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        });
        var hostsActions = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        var openHostsFolder = new Button
        {
            Content = Loc.T("service.hosts_open_folder"),
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            Margin = new Thickness(0, 0, 8, 8)
        };
        openHostsFolder.Click += (_, _) => OpenHostsFolder();
        var refreshHosts = new Button
        {
            Content = Loc.T("service.hosts_refresh"),
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            Margin = new Thickness(0, 0, 8, 8)
        };
        hostsActions.Children.Add(openHostsFolder);
        hostsActions.Children.Add(refreshHosts);
        hostsStack.Children.Add(hostsActions);
        var hostsStatus = new TextBlock
        {
            Text = Loc.T("service.hosts_loading"),
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 4, 0, 0)
        };
        hostsStack.Children.Add(hostsStatus);
        hostsCard.Child = hostsStack;
        root.Children.Add(hostsCard);
        var hostsRowsFrom = hostsStack.Children.Count;
        Loaded += async (_, _) => await LoadHostTogglesAsync(hostsStack, hostsStatus, refreshHosts, hostsRowsFrom, announce: false);
        refreshHosts.Click += async (_, _) => await LoadHostTogglesAsync(hostsStack, hostsStatus, refreshHosts, hostsRowsFrom, announce: true);

        // Updates
        root.Children.Add(Section(Loc.T("service.section_updates")));
        var updCard = Card();
        var updStack = new StackPanel();
        updStack.Children.Add(new TextBlock
        {
            Text = Loc.T("service.updates_hint"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 12)
        });
        var startupCheck = new CheckBox
        {
            Content = Loc.T("service.check_on_startup"),
            IsChecked = _settings.CheckUpdatesOnStartup,
            Margin = new Thickness(0, 0, 0, 12)
        };
        startupCheck.Checked += (_, _) => { _settings.CheckUpdatesOnStartup = true; _settings.Save(); };
        startupCheck.Unchecked += (_, _) => { _settings.CheckUpdatesOnStartup = false; _settings.Save(); };
        updStack.Children.Add(startupCheck);
        var flowsealAuto = new CheckBox
        {
            Content = Loc.T("service.auto_update"),
            IsChecked = _settingsSvc.IsAutoUpdateEnabled(),
            Margin = new Thickness(0, 0, 0, 12)
        };
        flowsealAuto.Checked += (_, _) => _settingsSvc.SetAutoUpdate(true);
        flowsealAuto.Unchecked += (_, _) => _settingsSvc.SetAutoUpdate(false);
        updStack.Children.Add(flowsealAuto);
        var updBtns = new WrapPanel();
        updBtns.Children.Add(ActionBtn(Loc.T("service.check_app_update"), async () => await CheckAppUpdateAsync()));
        updBtns.Children.Add(ActionBtn(Loc.T("service.check_flowseal_update"), async () => await CheckFlowsealUpdateAsync()));
        updBtns.Children.Add(ActionBtn(Loc.T("service.reinstall_flowseal"), async () => await ReinstallFlowsealAsync()));
        updStack.Children.Add(updBtns);
        updCard.Child = updStack;
        root.Children.Add(updCard);

        // Network reset
        root.Children.Add(Section(Loc.T("service.section_network")));
        var netCard = Card();
        var netStack = new StackPanel();
        netStack.Children.Add(new TextBlock
        {
            Text = Loc.T("service.network_desc"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 12)
        });
        netStack.Children.Add(new TextBlock
        {
            Text = Loc.T("service.network_cmds"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 12)
        });
        netStack.Children.Add(ActionBtn(Loc.T("service.reset_network"), async () => await ResetNetworkAsync()));
        netCard.Child = netStack;
        root.Children.Add(netCard);

        root.Children.Add(Section(Loc.T("service.section_exclusions")));
        var exCard = Card();
        var exStack = new StackPanel();
        exStack.Children.Add(new TextBlock
        {
            Text = Loc.T("service.exclusions_desc"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 12)
        });
        _componentStatus = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        exStack.Children.Add(_componentStatus);
        exStack.Children.Add(LinkButton(Loc.T("health.open_guide"), ZapretHealth.GuideUrl));
        exCard.Child = exStack;
        root.Children.Add(exCard);

        root.Children.Add(Section(Loc.T("service.section_user_files")));
        var userCard = Card();
        var userStack = new StackPanel();
        userStack.Children.Add(new TextBlock
        {
            Text = Loc.T("service.user_files_desc"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 12)
        });
        var userBtns = new WrapPanel();
        userBtns.Children.Add(ActionBtn(Loc.T("service.export_user"), ExportUserData));
        userBtns.Children.Add(ActionBtn(Loc.T("service.import_user"), ImportUserData));
        userStack.Children.Add(userBtns);
        userCard.Child = userStack;
        root.Children.Add(userCard);

        // Language
        root.Children.Add(Section(Loc.T("service.section_language")));
        var langCard = Card();
        var langStack = new StackPanel();
        langStack.Children.Add(new TextBlock
        {
            Text = Loc.T("service.language_desc"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 12)
        });
        var langCombo = new ComboBox
        {
            MinWidth = 240,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        langCombo.Items.Add(Loc.T("service.lang_ru"));
        langCombo.Items.Add(Loc.T("service.lang_en"));
        langCombo.SelectedIndex = string.Equals(_settings.Language, "en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        langCombo.SelectionChanged += (_, _) =>
        {
            var newLang = langCombo.SelectedIndex == 1 ? "en" : "ru";
            if (string.Equals(_settings.Language, newLang, StringComparison.OrdinalIgnoreCase)) return;
            _settings.Language = newLang;
            _settings.Save();
            LocalizationService.Initialize(newLang);
            if (Application.Current.MainWindow is MainWindow mainWindow)
                mainWindow.ApplyLanguageChange();
        };
        langStack.Children.Add(langCombo);
        langCard.Child = langStack;
        root.Children.Add(langCard);

        root.Children.Add(Section(Loc.T("service.section_theme")));
        var themeCard = Card();
        var themeStack = new StackPanel();
        themeStack.Children.Add(new TextBlock
        {
            Text = Loc.T("service.theme_desc"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 12)
        });
        var themeCombo = new ComboBox
        {
            MinWidth = 240,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        themeCombo.Items.Add(Loc.T("service.theme_windows"));
        themeCombo.Items.Add(Loc.T("service.theme_dark"));
        themeCombo.Items.Add(Loc.T("service.theme_light"));
        themeCombo.SelectedIndex = _settings.Theme switch
        {
            ThemeService.Windows => 0,
            ThemeService.Light => 2,
            _ => 1
        };
        themeCombo.SelectionChanged += (_, _) =>
        {
            var next = themeCombo.SelectedIndex switch
            {
                0 => ThemeService.Windows,
                2 => ThemeService.Light,
                _ => ThemeService.Dark
            };
            if (string.Equals(_settings.Theme, next, StringComparison.OrdinalIgnoreCase))
                return;
            _settings.Theme = next;
            _settings.Save();
            ThemeService.Apply(next);
            if (Application.Current.MainWindow is MainWindow window)
                window.ReloadActivePage();
        };
        themeStack.Children.Add(themeCombo);
        themeCard.Child = themeStack;
        root.Children.Add(themeCard);

        // Links
        root.Children.Add(Section(Loc.T("service.section_links")));
        var linksCard = Card();
        var linksRow = new WrapPanel();
        linksRow.Children.Add(LinkButton(Loc.T("service.link_flowseal"), FlowsealUrl));
        linksRow.Children.Add(LinkButton(Loc.T("service.link_aeroway"), AppUrl));
        linksRow.Children.Add(LinkButton("Telegram", TelegramUrl));
        linksRow.Children.Add(LinkButton("Донат", DonateUrl));
        linksCard.Child = linksRow;
        root.Children.Add(linksCard);

        scroll.Content = root;
        Content = scroll;
    }

    private async Task CheckAppUpdateAsync()
    {
        try
        {
            var updater = new AppSelfUpdateService(_settings, _paths.Root);
            var result = await UiHelpers.RunWithLoadingAsync(
                OwnerWindow,
                Loc.T("update.checking_app"),
                () => updater.CheckForUpdateAsync());
            if (result.Error is not null)
            {
                UiHelpers.ShowResult(OwnerWindow, Loc.T("dialog.update_app"), $"{Loc.T("common.error_prefix")} {result.Error}");
                return;
            }

            if (!result.HasUpdate)
            {
                UiHelpers.ShowResult(OwnerWindow, Loc.T("dialog.update_app"), Loc.F("update.app_up_to_date", result.LocalVersion));
                return;
            }

            if (UiHelpers.Confirm(Loc.F("update.app_available", result.RemoteVersion, result.LocalVersion), OwnerWindow))
            {
                if (result.Manifest is not null)
                {
                    PreparedAppUpdate? prepared = null;
                    var keepPrepared = false;
                    UpdateDownloadWindow? progressWin = new UpdateDownloadWindow(Loc.T("update.download_title"), Loc.T("update.download_status"));
                    if (OwnerWindow is not null) progressWin.Owner = OwnerWindow;
                    progressWin.Show();
                    try
                    {
                        var downloadProgress = new Progress<DownloadProgress>(p => progressWin.ReportProgress(p));
                        var preparedResult = await updater.PrepareUpdateAsync(
                            result.Manifest, downloadProgress);
                        if (!preparedResult.Success || preparedResult.Payload is null)
                        {
                            UiHelpers.ShowError(preparedResult.Message);
                            return;
                        }

                        prepared = preparedResult.Payload;
                        progressWin.SetStatus(Loc.T("update.download_complete"));

                        if (UiHelpers.Confirm(AppSelfUpdateService.GetInstallConfirmMessage(result.RemoteVersion), OwnerWindow))
                        {
                            progressWin.SetStatus(Loc.T("update.starting_install"));
                            progressWin.ReportProgress(new DownloadProgress
                            {
                                Phase = Loc.T("update.install_phase")
                            });
                            var install = await updater.InstallPreparedUpdateAsync(prepared);
                            progressWin.Close();
                            progressWin = null!;
                            keepPrepared = install.KeepPreparedFiles;
                            if (install.RequiresRestart && Application.Current.MainWindow is MainWindow mw)
                                mw.ShutdownApplication();
                            else if (!install.Success)
                                UiHelpers.ShowError(install.Message);
                            else
                                UiHelpers.ShowInfo(install.Message);
                        }
                    }
                    finally
                    {
                        progressWin?.Close();
                        AppSelfUpdateService.CleanupPreparedUpdate(prepared, keepPrepared);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            UiHelpers.ShowResult(OwnerWindow, Loc.T("dialog.update_app"), $"{Loc.T("common.error_prefix")} {ex.Message}");
        }
    }

    private async Task ReinstallFlowsealAsync()
    {
        if (!UiHelpers.Confirm(Loc.T("update.flowseal_reinstall"), OwnerWindow))
            return;

        await FlowsealReinstallService.ReinstallAsync(OwnerWindow, _paths);
    }

    private async Task CheckFlowsealUpdateAsync()
    {
        var r = await UiHelpers.RunWithLoadingAsync(
            OwnerWindow,
            Loc.T("update.checking_flowseal"),
            () => _updates.CheckForUpdatesAsync());
        if (r.Error is not null)
        {
            UiHelpers.ShowResult(OwnerWindow, Loc.T("dialog.update_flowseal"), $"{Loc.T("common.error_prefix")} {r.Error}");
            return;
        }

        if (r.IsUpToDate)
        {
            UiHelpers.ShowResult(OwnerWindow, Loc.T("dialog.update_flowseal"), Loc.F("update.flowseal_up_to_date", r.LocalVersion));
            return;
        }

        if (UiHelpers.Confirm(Loc.F("update.flowseal_available", r.RemoteVersion, r.LocalVersion), OwnerWindow))
            await FlowsealReinstallService.ReinstallAsync(OwnerWindow, _paths);
    }

    private async Task ResetNetworkAsync()
    {
        if (!UiHelpers.Confirm(Loc.T("network.reset_confirm"), OwnerWindow))
            return;

        try
        {
            var (ok, output) = await UiHelpers.RunWithLoadingAsync(
                OwnerWindow,
                Loc.T("common.loading"),
                () => NetworkResetService.RunAllAsync());
            UiHelpers.ShowResult(OwnerWindow, Loc.T("network.reset_title"), output + (ok ? Loc.T("network.reset_reboot") : ""));
        }
        catch (Exception ex)
        {
            UiHelpers.ShowResult(OwnerWindow, Loc.T("network.reset_title"), $"{Loc.T("common.error_prefix")} {ex.Message}");
        }
    }

    private async Task UpdateIpsetWithDialog()
    {
        try
        {
            await UiHelpers.RunWithLoadingAsync(
                OwnerWindow,
                Loc.T("common.loading"),
                () => _updates.UpdateIpsetListAsync());
            RefreshStatuses();
            UiHelpers.ShowResult(OwnerWindow, Loc.T("dialog.ipset_list"), Loc.T("dialog.ipset_ok"));
        }
        catch (Exception ex)
        {
            UiHelpers.ShowResult(OwnerWindow, Loc.T("dialog.ipset_list"), $"{Loc.T("common.error_prefix")} {ex.Message}");
        }
    }

    private int _hostsLoad;

    private async Task LoadHostTogglesAsync(StackPanel panel, TextBlock status, Button refresh, int rowsFrom, bool announce)
    {
        var load = ++_hostsLoad;
        refresh.IsEnabled = false;
        try
        {
            status.Text = Loc.T("service.hosts_loading");
            status.Visibility = Visibility.Visible;
            var snapshot = await _hostsCatalog.RefreshAppliedAsync();
            if (load != _hostsLoad)
                return;

            while (panel.Children.Count > rowsFrom)
                panel.Children.RemoveAt(panel.Children.Count - 1);

            if (snapshot.FromNetwork)
                status.Visibility = Visibility.Collapsed;
            else
            {
                status.Text = Loc.T("service.hosts_refresh_offline");
                status.Visibility = Visibility.Visible;
            }

            var lists = snapshot.Sections;
            if (announce && Window.GetWindow(this) is MainWindow window)
            {
                window.ShowToast(snapshot.UpdatedBlocks > 0
                    ? Loc.F("service.hosts_refresh_updated", snapshot.UpdatedBlocks)
                    : snapshot.FromNetwork
                        ? Loc.T("service.hosts_refresh_ok")
                        : Loc.T("service.hosts_refresh_offline"));
            }
            var hidden = new List<UIElement>();
            for (var i = 0; i < lists.Count; i++)
            {
                var row = HostToggleRow(lists[i]);
                if (i >= 5)
                {
                    row.Visibility = Visibility.Collapsed;
                    hidden.Add(row);
                }

                panel.Children.Add(row);
            }

            if (hidden.Count == 0)
                return;

            var more = new Button
            {
                Content = Loc.F("service.hosts_show_all", hidden.Count),
                Style = (Style)Application.Current.FindResource("SecondaryButton"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 12, 0, 0)
            };
            more.Click += (_, _) =>
            {
                var open = hidden[0].Visibility != Visibility.Visible;
                foreach (var item in hidden)
                    item.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
                more.Content = open
                    ? Loc.T("service.hosts_hide")
                    : Loc.F("service.hosts_show_all", hidden.Count);
            };
            panel.Children.Add(more);
        }
        catch (Exception ex)
        {
            if (load != _hostsLoad)
                return;
            status.Text = Loc.T("service.hosts_empty") + " " + ex.Message;
            status.Visibility = Visibility.Visible;
        }
        finally
        {
            if (load == _hostsLoad)
                refresh.IsEnabled = true;
        }
    }

    private StackPanel HostToggleRow(HostListSection list)
    {
        var root = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        var row = new DockPanel { LastChildFill = true };
        var toggle = new ToggleButton
        {
            Style = (Style)Application.Current.FindResource("SwitchToggle"),
            IsChecked = SystemHostsFile.Covers(list.Id, list.Lines),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        var show = new Button
        {
            Content = Loc.T("service.hosts_show_entries"),
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var preview = new TextEditor
        {
            Text = string.Join(Environment.NewLine, list.Lines),
            Height = 220,
            Margin = new Thickness(0, 6, 0, 0),
            Visibility = Visibility.Collapsed
        };
        HostsSyntaxHighlighting.Apply(preview);

        DockPanel.SetDock(toggle, Dock.Right);
        DockPanel.SetDock(show, Dock.Right);
        row.Children.Add(toggle);
        row.Children.Add(show);
        row.Children.Add(new TextBlock
        {
            Text = list.Title,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        show.Click += (_, _) =>
        {
            var open = preview.Visibility != Visibility.Visible;
            preview.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            show.Content = open ? Loc.T("service.hosts_hide_entries") : Loc.T("service.hosts_show_entries");
        };
        toggle.Checked += async (_, _) => await ApplyHostToggleAsync(toggle, list, true);
        toggle.Unchecked += async (_, _) => await ApplyHostToggleAsync(toggle, list, false);

        root.Children.Add(row);
        root.Children.Add(preview);
        return root;
    }

    private static void OpenHostsFolder()
    {
        var file = SystemHostsFile.DefaultPath;
        var folder = Path.GetDirectoryName(file);
        if (File.Exists(file))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
            return;
        }

        if (!string.IsNullOrEmpty(folder))
            UiHelpers.OpenFolder(folder);
    }

    private async Task ApplyHostToggleAsync(ToggleButton toggle, HostListSection list, bool enabled)
    {
        if (_hostsGuard)
            return;
        toggle.IsEnabled = false;
        try
        {
            if (enabled)
                await Task.Run(() => SystemHostsFile.SetBlock(list.Id, list.Lines));
            else
                await Task.Run(() => SystemHostsFile.RemoveEntries(list.Id, list.Lines));
        }
        catch (Exception ex)
        {
            _hostsGuard = true;
            toggle.IsChecked = !enabled;
            _hostsGuard = false;
            UiHelpers.ShowError(ex.Message);
        }
        finally
        {
            toggle.IsEnabled = true;
        }
    }

    private async Task UpdateHostsWithDialog()
    {
        try
        {
            var result = await UiHelpers.RunWithLoadingAsync(
                OwnerWindow,
                Loc.T("common.loading"),
                () => _updates.PrepareHostsUpdateAsync());

            if (result.Error is not null)
            {
                UiHelpers.ShowResult(OwnerWindow, Loc.T("dialog.update_hosts"), $"{Loc.T("common.error_prefix")} {result.Error}");
                return;
            }

            if (result.IsUpToDate)
            {
                UiHelpers.ShowResult(OwnerWindow, Loc.T("dialog.update_hosts"), Loc.T("dialog.hosts_up_to_date"));
                return;
            }

            if (result.NeedsManualMerge &&
                !string.IsNullOrWhiteSpace(result.TempFilePath) &&
                !string.IsNullOrWhiteSpace(result.SystemHostsPath))
            {
                UpdateService.OpenHostsMergeAssist(result.TempFilePath, result.SystemHostsPath);
                UiHelpers.ShowResult(OwnerWindow, Loc.T("dialog.update_hosts"), Loc.T("dialog.hosts_manual"));
            }
        }
        catch (Exception ex)
        {
            UiHelpers.ShowResult(OwnerWindow, Loc.T("dialog.update_hosts"), $"{Loc.T("common.error_prefix")} {ex.Message}");
        }
    }

    private Task ReplaceFakesAsync()
    {
        var svc = new FakeReplacementService(_paths);
        var status = svc.GetStatus();
        if (status.Error is not null)
        {
            UiHelpers.ShowResult(OwnerWindow, Loc.T("fake.title"), status.Error);
            return Task.CompletedTask;
        }

        if (status.AvailableFiles.Count == 0)
        {
            UiHelpers.ShowResult(OwnerWindow, Loc.T("fake.title"), Loc.T("fake.no_files"));
            return Task.CompletedTask;
        }

        if (!FakeReplacementWindow.TryShow(status, out var target, out var file, OwnerWindow))
            return Task.CompletedTask;

        try
        {
            svc.Replace(target, file!);
            var typeLabel = target == FakeTarget.DiscordUdp
                ? Loc.T("fake.type_discord")
                : Loc.T("fake.type_game");
            var fakeName = Path.GetFileNameWithoutExtension(file!) ?? file!;
            UiHelpers.ShowResult(OwnerWindow, Loc.T("fake.title"), Loc.F("fake.replaced", typeLabel, fakeName));
        }
        catch (Exception ex)
        {
            UiHelpers.ShowResult(OwnerWindow, Loc.T("fake.title"), $"{Loc.T("common.error_prefix")} {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private static Button LinkButton(string text, string url)
    {
        var btn = new Button
        {
            Content = text,
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        btn.Click += (_, _) => OpenUrl(url);
        return btn;
    }

    private void ApplyGameFilterMode(string mode)
    {
        TrySaveGameFilterPorts(showError: false);
        _settingsSvc.SetGameFilter(mode);
    }

    private void ApplyGameFilterPorts()
    {
        if (!TrySaveGameFilterPorts(showError: true))
            return;

        RefreshStatuses();
        ShowPortsAppliedFeedback();
        ConsoleLog.Instance.Write(Loc.T("service.game_filter_ports_saved"));
    }

    private void ShowPortsAppliedFeedback()
    {
        _gameFilterPortsFeedback.Text = Loc.T("service.game_filter_ports_saved");
        _gameFilterPortsFeedback.Foreground = (Brush)Application.Current.FindResource("SuccessBrush");
        _gameFilterPortsFeedback.Visibility = Visibility.Visible;
        _gameApplyPortsBtn.Style = (Style)Application.Current.FindResource("SuccessButton");

        _portsFeedbackTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _portsFeedbackTimer.Stop();
        _portsFeedbackTimer.Tick -= OnPortsFeedbackTick;
        _portsFeedbackTimer.Tick += OnPortsFeedbackTick;
        _portsFeedbackTimer.Start();
    }

    private void OnPortsFeedbackTick(object? sender, EventArgs e)
    {
        _portsFeedbackTimer?.Stop();
        _gameFilterPortsFeedback.Visibility = Visibility.Collapsed;
        _gameApplyPortsBtn.Style = (Style)Application.Current.FindResource("SecondaryButton");
    }

    private bool TrySaveGameFilterPorts(bool showError)
    {
        try
        {
            _settingsSvc.SetGameFilterPorts(_gameTcpPorts.Text, _gameUdpPorts.Text);
            return true;
        }
        catch
        {
            if (showError)
                UiHelpers.ShowError(Loc.T("service.game_filter_ports_invalid"));
            return false;
        }
    }

    private void SetIpsetMode(string mode)
    {
        try
        {
            _settingsSvc.SetIpsetFilter(mode);
            RefreshStatuses();
            ConsoleLog.Instance.Write(Loc.F("service.ipset_set", mode));
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(ex.Message);
        }
    }

    private void RefreshStatuses()
    {
        var game = _settingsSvc.GetGameFilter();
        SetStatusText(_gameFilterStatus, "Game Filter", _settingsSvc.GetGameFilterStatus());
        _gameFilterPortsApplied.Text = Loc.F("service.game_filter_ports_applied", game.TcpRange, game.UdpRange);
        SetStatusText(_ipsetStatus, "IPSet Filter", _settingsSvc.GetIpsetStatus());
        _updatingPortBoxes = true;
        _gameTcpPorts.Text = game.TcpRange;
        _gameUdpPorts.Text = game.UdpRange;
        _updatingPortBoxes = false;
        UpdatePortBoxVisual(_gameTcpPorts);
        UpdatePortBoxVisual(_gameUdpPorts);

        var gameMode = game.Mode;
        ApplyActiveStyle(_gameDisabledBtn, gameMode == "disabled");
        ApplyActiveStyle(_gameTcpUdpBtn, gameMode == "all");
        ApplyActiveStyle(_gameTcpBtn, gameMode == "tcp");
        ApplyActiveStyle(_gameUdpBtn, gameMode == "udp");

        var ipsetMode = _settingsSvc.GetIpsetStatus();
        ApplyActiveStyle(_ipsetLoadedBtn, ipsetMode == "loaded");
        ApplyActiveStyle(_ipsetNoneBtn, ipsetMode == "none");
        ApplyActiveStyle(_ipsetAnyBtn, ipsetMode == "any");

        var missing = ZapretHealth.MissingFiles(_paths);
        if (missing.Count == 0)
        {
            _componentStatus.Visibility = Visibility.Collapsed;
        }
        else
        {
            _componentStatus.Visibility = Visibility.Visible;
            _componentStatus.Text = Loc.F("strategy.files_missing", string.Join(", ", missing));
            _componentStatus.Foreground = (Brush)Application.Current.FindResource("ErrorBrush");
        }
    }

    private void ExportUserData()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Zip (*.zip)|*.zip",
            FileName = "Aeroway-user.zip"
        };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            UserDataPack.Export(_paths.Root, dialog.FileName);
            UiHelpers.ShowInfo(Loc.T("service.export_done"));
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(ex.Message);
        }
    }

    private void ImportUserData()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Zip (*.zip)|*.zip"
        };
        if (dialog.ShowDialog() != true)
            return;
        if (!UiHelpers.Confirm(Loc.T("service.import_confirm"), OwnerWindow))
            return;

        try
        {
            var count = UserDataPack.Import(_paths.Root, dialog.FileName);
            UiHelpers.ShowInfo(Loc.F("service.import_done", count));
            RefreshStatuses();
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(ex.Message);
        }
    }

    private static TextBlock Section(string text) => new()
    {
        Text = text,
        FontSize = 16,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 8, 0, 8)
    };

    private static Border Card()
    {
        var card = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12)
        };
        card.SetResourceReference(Border.BackgroundProperty, "PanelOverlayBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        return card;
    }

    private static TextBlock Label(string text) => new() { Text = text, Margin = new Thickness(0, 0, 0, 4) };

    private TextBox PortRangeBox()
    {
        var box = new TextBox
        {
            MinWidth = 260,
            MaxLength = 80,
            Padding = new Thickness(8, 6, 8, 6),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        InputMethod.SetIsInputMethodEnabled(box, false);
        box.PreviewTextInput += OnPortRangePreviewTextInput;
        box.PreviewKeyDown += OnPortRangePreviewKeyDown;
        DataObject.AddPastingHandler(box, OnPortRangePasting);
        box.TextChanged += OnPortRangeTextChanged;
        return box;
    }

    private void OnPortRangePreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !ServiceSettingsService.IsPortRangeInputChar(e.Text);

    private static void OnPortRangePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Space)
            e.Handled = true;
    }

    private static void OnPortRangePasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            e.CancelCommand();
            return;
        }

        var pasted = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string ?? "";
        var sanitized = ServiceSettingsService.SanitizePortRangeInput(pasted);
        if (sanitized.Length == 0)
        {
            e.CancelCommand();
            return;
        }

        if (sanitized == pasted)
            return;

        e.CancelCommand();
        var start = box.SelectionStart;
        var next = box.Text.Remove(start, box.SelectionLength).Insert(start, sanitized);
        box.Text = next;
        box.CaretIndex = start + sanitized.Length;
    }

    private void OnPortRangeTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingPortBoxes || sender is not TextBox box)
            return;

        var sanitized = ServiceSettingsService.SanitizePortRangeInput(box.Text);
        if (sanitized != box.Text)
        {
            var caret = Math.Min(box.CaretIndex, sanitized.Length);
            _updatingPortBoxes = true;
            box.Text = sanitized;
            box.CaretIndex = caret;
            _updatingPortBoxes = false;
        }

        UpdatePortBoxVisual(box);
    }

    private static void UpdatePortBoxVisual(TextBox box)
    {
        var compact = ServiceSettingsService.CompactPortRange(box.Text);
        var looksComplete = compact.Length > 0 && compact[^1] is not '-' and not ',';
        var valid = !looksComplete || ServiceSettingsService.TryNormalizePortRange(compact, out _);
        box.BorderBrush = valid
            ? (Brush)Application.Current.FindResource("BorderBrush")
            : (Brush)Application.Current.FindResource("WarningBrush");
    }

    private static StackPanel PortRangeRow(string label, TextBox box)
    {
        var row = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        row.Children.Add(new TextBlock
        {
            Text = label,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush")
        });
        row.Children.Add(box);
        return row;
    }

    private static TextBlock StatusLine(string name) => new()
    {
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 4)
    };

    private static void SetStatusText(TextBlock block, string label, string value)
    {
        block.Inlines.Clear();
        block.Inlines.Add(new Run($"{label}: ")
        {
            Foreground = (Brush)Application.Current.FindResource("TextMutedBrush")
        });
        block.Inlines.Add(new Run(value)
        {
            Foreground = GetStatusValueBrush(value),
            FontWeight = FontWeights.SemiBold
        });
    }

    private static Brush GetStatusValueBrush(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("enabled", StringComparison.Ordinal) || normalized is "on" or "yes" or "true")
            return (Brush)Application.Current.FindResource("SuccessBrush");
        if (normalized is "disabled" or "off" or "no" or "false")
            return (Brush)Application.Current.FindResource("WarningBrush");
        if (normalized is "loaded" or "any")
            return (Brush)Application.Current.FindResource("SuccessBrush");
        if (normalized is "none")
            return (Brush)Application.Current.FindResource("WarningBrush");
        return (Brush)Application.Current.FindResource("AccentBrush");
    }

    private Button SettingsBtn(string text, Action action)
    {
        var btn = new Button
        {
            Content = text,
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            Margin = new Thickness(0, 0, 8, 8)
        };
        btn.Click += (_, _) =>
        {
            action();
            RefreshStatuses();
        };
        return btn;
    }

    private static void ApplyActiveStyle(Button btn, bool active) =>
        btn.Style = (Style)Application.Current.FindResource(active ? "SuccessButton" : "SecondaryButton");

    private Button ActionBtn(string text, Action action) => ActionBtn(text, () => { action(); return Task.CompletedTask; });

    private Button ActionBtn(string text, Func<Task> action)
    {
        var btn = new Button
        {
            Content = text,
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            Margin = new Thickness(0, 0, 8, 8)
        };
        btn.Click += async (_, _) => await action();
        return btn;
    }
}
