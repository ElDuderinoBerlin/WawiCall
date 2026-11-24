using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Configuration;
using System.Collections.Immutable;
using System.ComponentModel;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.App.Views;

public partial class SettingsPage : XtraUserControl
{
    private SipAdvancedSettings _sipAdvanced = new();
    private AppWorkspace? _workspace;
    private ConfigurationSnapshot? _snapshot;
    private PageOperation? _operation;
    private List<CacheCompanyRow> _cacheCompanies = [];
    private AppUiState? _state;
    private CancellationTokenSource? _cacheCancellation;
    private Func<Task>? _beforePluginSwitch;
    private Func<Task>? _afterPluginSwitch;
    private bool _arrangingButtons;
    private bool _initialBindingPending;
    private bool _settingsLoaded;
    private int _cacheActionColumns;
    private int _cacheActionCellWidth;
    private AutoStartState? _autoStartState;
    internal WawiCallPro.Utilities.Operations.OperationEvent? LastSettingsNotice { get; private set; }
    internal string LastSettingsNoticeText => LastSettingsNotice is { } notice ? WawiCallPro.Utilities.Operations.SettingsMessages.Get(notice) ?? "" : "";
    private void ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent notice)
    { LastSettingsNotice = notice; _workspace?.ReportSettings(notice); }
    internal event EventHandler? RestartRequested;
    internal event EventHandler? SaveCompleted;
    internal event EventHandler? CloseRequested;
    internal event EventHandler? ContentSizeChanged;
    internal int GetMinimumWindowHeight()
    {
        if (tabs.SelectedTabPage == sipTab || tabs.SelectedTabPage == generalTab || tabs.SelectedTabPage == audioTab) return GetRequiredGeneralHeight();
        var width = Math.Max(1, layout.ClientSize.Width - layout.Padding.Horizontal);

        return layout.Padding.Vertical + tabs.Margin.Vertical + generalTab.Top + generalTab.Left + Font.Height * 4
            + footer.GetPreferredSize(new(width, 0)).Height + footer.Margin.Vertical;
    }
    internal int GetRequiredGeneralHeight()
    {
        var width = Math.Max(1, tabs.ClientSize.Width);
        var content = Math.Max(generalLayout.GetPreferredSize(new(width, 0)).Height,
            Math.Max(audioLayout.GetPreferredSize(new(width, 0)).Height, audioLevelsLayout.GetPreferredSize(new(width, 0)).Height)
            + audioDevicesTab.Top + audioDevicesTab.Left);
        if (tabs.SelectedTabPage == sipTab)
            content = Math.Max(content, sipProfilesLayout.GetPreferredSize(new(width, 0)).Height
                + sipLayout.GetPreferredSize(new(width, 0)).Height
                + sipStandard.Top + sipStandard.Left);
        if (tabs.SelectedTabPage == mailTab) content = Math.Max(content, mailSettingsPage.GetRequiredHeight());
        if (tabs.SelectedTabPage == wawiTab) content = Math.Max(content, wawiLayout.GetPreferredSize(new(width, 0)).Height);

        // Hidden pages may retain an old Height. Derive chrome from content and stable header/border offsets instead.
        return content + layout.Padding.Vertical + tabs.Margin.Vertical + generalTab.Top + generalTab.Left
            + footer.GetPreferredSize(new(layout.ClientSize.Width, 0)).Height + footer.Margin.Vertical;
    }
    public SettingsPage()
    {
        InitializeComponent();
        SetSipPasswordVisibility(false);
        databaseLayout.Layout += DatabaseLayout_Changed;
        footer.SizeChanged += ButtonLayout_Changed;
        footer.Layout += General_Layout;
        generalLayout.Layout += General_Layout;
        tabs.SelectedPageChanged += SettingsTab_Changed;
        audioLayout.Layout += General_Layout;
        audioLevelsLayout.Layout += General_Layout;
        sipProfilesLayout.Layout += General_Layout;
        sipLayout.Layout += General_Layout;
        UiButtonSizing.Attach(this);
        transportEdit.Properties.Items.AddRange(new object[] { "UDP", "TCP", "TLS" });
        FontChanged += ButtonLayout_Changed;
        cacheLayout.SizeChanged += ButtonLayout_Changed;
        matchingLayout.SizeChanged += ButtonLayout_Changed;
        var appearance = LookAndFeel;
        appearance.StyleChanged += ButtonLayout_Changed;
        Disposed += (_, _) => appearance.StyleChanged -= ButtonLayout_Changed;
        ArrangeButtons();
    }
    private void General_Layout(object? sender, LayoutEventArgs e) => ContentSizeChanged?.Invoke(this, EventArgs.Empty);
    private void SettingsTab_Changed(object? sender, DevExpress.XtraTab.TabPageChangedEventArgs e)
    {
        ArrangeButtons();
        ContentSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ButtonLayout_Changed(object? sender, EventArgs e) => ArrangeButtons();
    private void ArrangeButtons()
    {
        if (_arrangingButtons || IsDisposed) return;
        _arrangingButtons = true;
        try
        {
            footer.SuspendLayout(); cacheActions.SuspendLayout(); matchingButtons.SuspendLayout();
            var actionMargin = new Padding(LogicalToDeviceUnits(6));
            foreach (var button in new[] { saveButton, cancelButton, importButton, exportButton }) button.Margin = actionMargin;
            footer.Padding = new Padding(LogicalToDeviceUnits(6), LogicalToDeviceUnits(6), LogicalToDeviceUnits(6), 0);
            UiButtonSizing.Apply(saveButton, cancelButton, importButton, exportButton);
            ArrangeTransferActions();
            UiButtonSizing.Apply(logDirectoryBrowseButton);
            UiButtonSizing.Apply(databaseSetupButton);
            UiButtonSizing.Apply(refreshAudioButton);
            UiButtonSizing.Apply(previewRingtoneButton, browseRingtoneButton);
            refreshAudioButton.Margin = new Padding(captureDeviceEdit.Margin.Left, refreshAudioButton.Margin.Top, refreshAudioButton.Margin.Right, refreshAudioButton.Margin.Bottom);
            var cacheButtons = new[] { loadCompaniesButton, refreshCacheButton, cancelCacheButton };
            foreach (var button in new[] { loadMatchingCompaniesButton, loadMatchingRuleButton, saveMatchingRuleButton })
            {
                button.Margin = new Padding(button.LogicalToDeviceUnits(4));
            }
            UiButtonSizing.Apply(loadMatchingCompaniesButton, loadMatchingRuleButton, saveMatchingRuleButton);
            UiButtonSizing.Apply(sipConnectButton, sipDisconnectButton);
            UiButtonSizing.Apply(addSipProfileButton, removeSipProfileButton);
            var cacheSize = UiButtonSizing.Apply(cacheButtons);
            var cellWidth = cacheSize.Width + loadCompaniesButton.Margin.Horizontal;
            var columns = Math.Clamp((cacheLayout.ClientSize.Width - cacheLayout.Padding.Horizontal) / Math.Max(1, cellWidth), 1, 3);
            if (columns != _cacheActionColumns || cellWidth != _cacheActionCellWidth)
            {
                _cacheActionColumns = columns;
                _cacheActionCellWidth = cellWidth;
                cacheActions.ColumnCount = columns; cacheActions.RowCount = (cacheButtons.Length + columns - 1) / columns;
                cacheActions.ColumnStyles.Clear(); cacheActions.RowStyles.Clear();
                for (var i = 0; i < columns; i++) cacheActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, cellWidth));
                for (var i = 0; i < cacheActions.RowCount; i++) cacheActions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                for (var i = 0; i < cacheButtons.Length; i++) cacheActions.SetCellPosition(cacheButtons[i], new(i % columns, i / columns));
            }
            footer.ResumeLayout(true); cacheActions.ResumeLayout(true); matchingButtons.ResumeLayout(true);
        }
        finally { _arrangingButtons = false; }
    }

    internal void Bind(AppWorkspace workspace, bool deferInitialBinding = false)
    {
        _workspace = workspace;
        databaseSetupButton.Enabled = true;
        if (workspace.Telephony is not null) workspace.Telephony.RegistrationChanged += SipRegistration_Changed;
        UpdateSipStatus();
        _operation = new PageOperation(this, ReportSettingsNotice);
        _ringtoneOperation = new PageOperation(this, audioStatus);
        mailSettingsPage.Bind(workspace, SaveMailSettingsAsync, ReloadMailSettingsAsync);
        pluginPage.Bind(workspace, _state, ReportSettingsNotice);
        pluginPage.ConfigureSelection(ActivatePluginAsync);
        if (workspace.FunctionPlugins is { } functions) functionPluginsPage.Bind(functions, ReportSettingsNotice);
        else functionPluginsTab.PageVisible = false;
        numberDiagnosticsPage.Bind(workspace.NumberDiagnostics, _state);
        if (deferInitialBinding)
        {
            _initialBindingPending = true;
            tabs.Enabled = saveButton.Enabled = importButton.Enabled = exportButton.Enabled = false;
            return;
        }
        Apply(workspace.Configuration.Current);
        LoadAutoStart();
        pluginPage.RefreshCatalog();
        cacheTab.PageEnabled = workspace.CanUseBusinessDirectory;
    }
    internal async Task StopAsync()
    {
        databaseSetupButton.Enabled = false;
        importButton.Enabled = exportButton.Enabled = false;
        if (_databaseDialog is { } databaseDialog)
        {
            await databaseDialog.StopAsync();
            databaseDialog.Close();
        }
        _audioStopped = true;
        SetDeviceEditorsEnabled(false); refreshAudioButton.Enabled = false;
        if (_ringtoneOperation is not null) await _ringtoneOperation.StopAsync();
        if (_operation is not null) await _operation.StopAsync();
        passwordEdit.Text = "";
        SetSipPasswordVisibility(false);
        await ClosePluginConnectionEditorAsync();
        await pluginPage.StopAsync();
        await functionPluginsPage.StopAsync();
        await numberDiagnosticsPage.StopAsync();
        await mailSettingsPage.StopAsync();
    }
    internal void ConfigurePluginSwitch(Func<Task>? beforeSwitch, Func<Task>? afterSwitch = null)
    {
        _beforePluginSwitch = beforeSwitch;
        _afterPluginSwitch = afterSwitch;
    }
    internal void SelectPluginsTab()
    {
        tabs.SelectedTabPage = pluginTab;
        pluginPages.SelectedTabPage = productPluginsTab;
    }
    internal void SelectAutomationTab(string id)
    {
        if (id == "plugin")
        {
            SelectPluginsTab();
            return;
        }
        if (id == "settings-wawi")
        {
            if (tabs.TabPages.Contains(wawiTab)) tabs.SelectedTabPage = wawiTab;
            else _selectPluginConnectionOnLoad = true;
            return;
        }
        tabs.SelectedTabPage = id switch
        {
            "settings-wawi" => wawiTab, "settings-hotkeys" => hotkeysTab,
            "settings-sip" or "settings-sip-advanced" => sipTab,
            "settings-database" => databaseTab, "settings-audio" or "settings-audio-levels" => audioTab,
            "settings-phone-cache" or "settings-cache-sources" or "settings-phone-matching" or "number-diagnostics" => cacheTab,
            "settings-mail" => mailTab, _ => generalTab
        };
        if (id.StartsWith("settings-sip", StringComparison.Ordinal))
            sipPages.SelectedTabPage = id == "settings-sip-advanced" ? sipAdvanced : sipStandard;
        if (id is "settings-audio" or "settings-audio-levels") audioPages.SelectedTabPage = id == "settings-audio-levels" ? audioLevelsTab : audioDevicesTab;
        if (id is "settings-phone-cache" or "settings-cache-sources" or "settings-phone-matching" or "number-diagnostics")
            cachePages.SelectedTabPage = id == "number-diagnostics" ? diagnosticsTab : id == "settings-phone-matching" ? matchingTab : cacheSourcesTab;
    }
    internal void SelectMailTab() => tabs.SelectedTabPage = mailTab;
    internal DevExpress.XtraTab.XtraTabControl SettingsTabs => tabs;
    private async Task ActivatePluginAsync(string id, CancellationToken token)
    {
        if (_workspace?.Plugins is not WawiCallPro.Interfaces.Plugins.IProductPluginSelection selection) return;
        RequireCleanPluginDraft();
        saveButton.Enabled = importButton.Enabled = exportButton.Enabled = false;
        foreach (DevExpress.XtraTab.XtraTabPage tab in tabs.TabPages) tab.PageEnabled = ReferenceEquals(tab, pluginTab);
        functionPluginsTab.PageEnabled = false;
        var switchPrepared = false;
        try
        {
            if (_operation is not null) await _operation.StopAsync();
            await ClosePluginConnectionEditorAsync();
            if (_beforePluginSwitch is not null) await _beforePluginSwitch();
            switchPrepared = true;
            await _workspace.Companies.SelectAsync(null);
            await selection.SwitchAsync(id, token);
            var previous = _snapshot!;
            await _workspace.Settings.SaveAsync(previous.Options with { User = previous.Options.User with { ActivePluginId = id } }, previous, token);
            _snapshot = await _workspace.Settings.LoadAsync(token);
            ApplyPluginConnectionSnapshot(_snapshot);
            await LoadPluginConnectionEditorAsync(token);
        }
        finally
        {
            _operation?.Dispose(); _operation = new PageOperation(this, ReportSettingsNotice);
            _cacheCompanies.Clear(); BindCompanyRows(); ClearMatchingProfile();
            saveButton.Enabled = importButton.Enabled = exportButton.Enabled = true;
            foreach (DevExpress.XtraTab.XtraTabPage tab in tabs.TabPages) tab.PageEnabled = true;
            functionPluginsTab.PageEnabled = true;
            cacheTab.PageEnabled = _workspace.CanUseBusinessDirectory;
            if (switchPrepared && _afterPluginSwitch is not null) await _afterPluginSwitch();
        }
    }
    private void DisposePage()
    {
        _audioStopped = true;
        StopAudioMeters();
        passwordEdit.Text = "";
        _operation?.Dispose();
        _ringtoneOperation?.Dispose();
        if (_workspace?.Telephony is not null) _workspace.Telephony.RegistrationChanged -= SipRegistration_Changed;
        if (!_sipUiLifetime.IsCancellationRequested) { _sipUiLifetime.Cancel(); _sipUiLifetime.Dispose(); }
        _state?.Save(cacheView, "CacheSources");
    }

    internal void ConfigureLayoutPersistence(AppUiState? state)
    {
        _state = state;
        _state?.Restore(cacheView, "CacheSources");
        pluginPage.ConfigureLayoutPersistence(state);
        functionPluginsPage.ConfigureLayoutPersistence(state);
        numberDiagnosticsPage.ConfigureLayoutPersistence(state);
        mailSettingsPage.ConfigureLayoutPersistence(state);
    }

    internal void Apply(ConfigurationSnapshot snapshot)
    {
        mailSettingsPage.Apply(snapshot.Options.MailAccounts, snapshot.Options.Companies);
        mailSettingsPage.ApplyCache(snapshot.Options.MailCache);
        _snapshot = snapshot;
        ApplyPluginConnectionSnapshot(snapshot);
        var options = snapshot.Options;
        notificationsEdit.Checked = options.User.ShowCallNotifications;
        minimizedEdit.Checked = options.User.StartMinimized;
        loggingEnabledEdit.Checked = options.User.LoggingEnabled;
        logLevelEdit.SelectedIndex = (int)options.User.LogLevel;
        logDirectoryEdit.Text = options.User.LogDirectory ?? _workspace?.Paths.LogsDirectory ?? "";
        showWindowHotkeyEdit.SelectedIndex = GlobalHotkeySettings.GetFunctionKey(options.User.ShowWindowHotkey);
        dialHotkeyEdit.SelectedIndex = GlobalHotkeySettings.GetFunctionKey(options.User.DialHotkey);
        ApplySipProfiles(options);
        ApplyDatabase(options.Database);
        playVolumeEdit.Value = options.Devices.PlaybackVolumePercent;
        ringVolumeEdit.Value = options.Devices.RingVolumePercent;
        mutedEdit.Checked = options.Devices.MicrophoneMuted;
        automaticRecordingEdit.Checked = options.Devices.RecordConnectedCallsAutomatically;
        recordingFormatEdit.SelectedIndex = (int)options.GetRecordingOutput().Format - 1;
        recordingSourceEdit.SelectedIndex = (int)options.Devices.RecordingReceiveSource;
        ApplyAudioDevices(options.Devices);
        var known = _cacheCompanies.Select(row => row.Key).ToHashSet();
        _cacheCompanies.AddRange(options.Companies.Where(company => !known.Contains(company.Company))
            .Select(company => new CacheCompanyRow(company.Company, company.DisplayName ?? "Gespeicherter Mandant", company.IncludeInPhoneCache, false)));
        foreach (var row in _cacheCompanies) row.Selected = options.FindCompanySettings(row.Key)?.IncludeInPhoneCache == true;
        BindCompanyRows();
        ReportSettingsNotice(snapshot.FileFound ? WawiCallPro.Utilities.Operations.OperationEvent.SettingsLoaded : WawiCallPro.Utilities.Operations.OperationEvent.SettingsDefaultsLoaded);
    }

    internal ApplicationOptions ReadOptions()
    {
        sipProperties.CloseEditor();
        var old = _snapshot!.Options;
        var previousOutput = old.GetRecordingOutput();
        var selectedFormat = (WawiCallPro.Database.Contracts.RecordingArtifactFormat)(recordingFormatEdit.SelectedIndex + 1);
        var output = selectedFormat == previousOutput.Format ? previousOutput
            : new RecordingOutputSettings(selectedFormat, recordingFormatEdit.SelectedIndex == 0 ? null : DateTimeOffset.UtcNow);
        var options = old with
        {
            User = old.User with { LoggingEnabled = loggingEnabledEdit.Checked, ShowCallNotifications = notificationsEdit.Checked, StartMinimized = minimizedEdit.Checked,
                LogLevel = (Microsoft.Extensions.Logging.LogLevel)logLevelEdit.SelectedIndex,
                LogDirectory = string.IsNullOrWhiteSpace(logDirectoryEdit.Text) || logDirectoryEdit.Text.Trim() == _workspace?.Paths.LogsDirectory ? null : logDirectoryEdit.Text.Trim(),
                ShowWindowHotkey = showWindowHotkeyEdit.SelectedIndex > 0 ? "Ctrl+Alt+F" + showWindowHotkeyEdit.SelectedIndex : null,
                DialHotkey = dialHotkeyEdit.SelectedIndex > 0 ? "Ctrl+Alt+F" + dialHotkeyEdit.SelectedIndex : null },
            Devices = old.Devices with { PlaybackVolumePercent = (int)playVolumeEdit.Value, RingVolumePercent = (int)ringVolumeEdit.Value, MicrophoneMuted = mutedEdit.Checked,
                RecordConnectedCallsAutomatically = automaticRecordingEdit.Checked,
                RecordingReceiveSource = (RecordingReceiveSource)recordingSourceEdit.SelectedIndex, CaptureDeviceId = ReadDevice(captureDeviceEdit), PlaybackDeviceId = ReadDevice(playbackDeviceEdit), RingDeviceId = ReadDevice(ringDeviceEdit),
                WindowsRingtoneFile = SelectedWindowsRingtone, CustomRingtonePath = SelectedCustomRingtone },
            RecordingOutput = output,
            AutomaticMp3 = new(),
            Companies = ReadCompanySettings(old),
            MailAccounts = mailSettingsPage.ReadAccounts(),
            MailCache = mailSettingsPage.ReadCache()
        };
        return ApplyPluginConnectionDraft(ReadSipProfiles(options));
    }

    private ImmutableArray<CompanySettings> ReadCompanySettings(ApplicationOptions old)
    {
        cacheView.CloseEditor();
        cacheView.UpdateCurrentRow();
        var settings = old.Companies.ToDictionary(item => item.Company);
        foreach (var row in _cacheCompanies)
        {
            if (settings.TryGetValue(row.Key, out var existing)) settings[row.Key] = existing with { IncludeInPhoneCache = row.Selected, DisplayName = row.FromSource ? row.Mandant : existing.DisplayName };
            else if (row.Selected) settings.Add(row.Key, new CompanySettings { Company = row.Key, IncludeInPhoneCache = true, DisplayName = row.Mandant });
        }
        return settings.Values.ToImmutableArray();
    }

    private async void LoadCompanies_Click(object? sender, EventArgs e)
    {
        if (_operation is null) return;
        await _operation.RunAsync(async token =>
        {
            var companies = await _workspace!.Directory.GetCompaniesAsync(token);
            var selection = _cacheCompanies.ToDictionary(row => row.Key, row => row.Selected);
            var missing = _cacheCompanies.Where(row => !companies.Any(company => company.Key == row.Key))
                .Select(row => new CacheCompanyRow(row.Key, row.Mandant, row.Selected, false) { CacheStatus = row.CacheStatus }).ToArray();
            _cacheCompanies = companies.Select(company => new CacheCompanyRow(company.Key, company.DisplayName,
                selection.TryGetValue(company.Key, out var selected) ? selected : _snapshot!.Options.FindCompanySettings(company.Key)?.IncludeInPhoneCache == true)).Concat(missing).ToList();
            BindCompanyRows();
            ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsCompaniesLoaded);
        });
    }

    private void BindCompanyRows()
    {
        cacheGrid.DataSource = _cacheCompanies;
        cacheGrid.RefreshDataSource();
        BindMatchingCompanies();
    }

    internal bool HasLoadedSettings => _workspace is null || _settingsLoaded;
    internal Task ReloadAsync() => _operation?.RunAsync(async token =>
    {
        RequireCleanPluginDraft();
        var workspace = _workspace!;
        var snapshot = await Task.Run(() => workspace.Settings.LoadAsync(token), token);
        token.ThrowIfCancellationRequested();
        Apply(snapshot);
        LoadAutoStart();
        var initial = _initialBindingPending;
        var catalog = Task.CompletedTask;
        if (initial)
        {
            _initialBindingPending = false;
            cacheTab.PageEnabled = workspace.CanUseBusinessDirectory;
            tabs.Enabled = true;
            catalog = pluginPage.RefreshCatalogAsync(token);
        }
        try
        {
            await Task.WhenAll(catalog, LoadAudioDevicesAsync(token), LoadPluginConnectionEditorAsync(token), functionPluginsPage.LoadAsync(token));
            token.ThrowIfCancellationRequested();
            _settingsLoaded = true;
        }
        finally
        {
            if (initial && !IsDisposed && !token.IsCancellationRequested)
                saveButton.Enabled = importButton.Enabled = exportButton.Enabled = true;
        }
    }) ?? Task.CompletedTask;
    private async Task ReloadMailSettingsAsync(CancellationToken token)
    {
        var next = await _workspace!.Settings.LoadAsync(token);
        // Refresh mail state without silently accepting concurrent edits to the other open settings tabs.
        var previousGeneral = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(_snapshot!.Options with { MailAccounts = [], MailCache = new() });
        var nextGeneral = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(next.Options with { MailAccounts = [], MailCache = new() });
        if (!previousGeneral.AsSpan().SequenceEqual(nextGeneral)) throw new WawiCallPro.Interfaces.Office.MailException(WawiCallPro.Interfaces.Office.MailFailure.Conflict);
        _snapshot = next;
        mailSettingsPage.Apply(_snapshot.Options.MailAccounts, _snapshot.Options.Companies);
        mailSettingsPage.ApplyCache(_snapshot.Options.MailCache);
    }
    private async Task SaveMailSettingsAsync(CancellationToken token)
    {
        var options = _snapshot!.Options with { MailAccounts = mailSettingsPage.ReadAccounts(), MailCache = mailSettingsPage.ReadCache() };
        await _workspace!.Settings.SaveAsync(options, _snapshot, token);
        await ReloadMailSettingsAsync(token);
    }
    private void LoadAutoStart()
    {
        if (_workspace is null) return;
        _autoStartState = _workspace.AutoStart.Read();
        autoStartEdit.Checked = _autoStartState.Enabled;
        autoStartEdit.Enabled = _autoStartState.CanChange;
        autoStartStatus.Text = _autoStartState.Status;
    }

    private sealed class CacheCompanyRow(CompanyKey key, string mandant, bool selected, bool fromSource = true)
    {
        [Browsable(false)] public CompanyKey Key { get; } = key;
        [Browsable(false)] public bool FromSource { get; } = fromSource;
        public string Mandant { get; } = mandant;
        public bool Selected { get; set; } = selected;
        public string CacheStatus { get; set; } = "Status nicht geprüft";
    }

    internal async Task SaveAsync(CancellationToken token)
    {
        sipContainer.Enabled = false;
        try
        {
            var startWithWindows = autoStartEdit.Checked;
            if (_pluginSettings is not null) await _pluginSettings.ValidateAsync(token);
            var publishCompany = _pluginPageContext?.HasSelectionChanges == true;
            var company = _pluginPageContext is null ? null : await _pluginPageContext.ValidateSelectionAsync(token);
            var options = ReadOptions();
            await _workspace!.Settings.SaveAsync(options, _snapshot!, token);
            Apply(await _workspace.Settings.LoadAsync(token));
            if (publishCompany) await _workspace.Companies.SelectAsync(company);
            if (_autoStartState is { CanChange: true } && startWithWindows != _autoStartState.Enabled)
            {
                token.ThrowIfCancellationRequested();
                try { _autoStartState = _workspace.AutoStart.Set(startWithWindows, _autoStartState); }
                catch (AutoStartException exception)
                {
                    autoStartStatus.Text = exception.Message;
                    throw new AutoStartException("Allgemeine Einstellungen gespeichert. " + exception.Message);
                }
                LoadAutoStart();
            }
            ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsSaved);
        }
        finally { if (!IsDisposed) sipContainer.Enabled = true; }
    }

    private async void Save_Click(object? sender, EventArgs e)
    {
        if (_operation is null || _operation.IsRunning) return;
        var saved = false;
        await _operation.RunAsync(async token =>
        {
            await SaveAndOfferRestartAsync(() => XtraMessageBox.Show(FindForm(),
            "Die geänderten Tastenkürzel wurden gespeichert und werden nach einem Neustart wirksam. Möchten Sie WawiCallPro jetzt neu starten?",
            "Neustart erforderlich", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes, token);
            saved = true;
        });
        if (saved) SaveCompleted?.Invoke(this, EventArgs.Empty);
    }
    private void Cancel_Click(object? sender, EventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void LoggingEnabled_Changed(object? sender, EventArgs e) => logLevelEdit.Enabled = logDirectoryEdit.Enabled = logDirectoryBrowseButton.Enabled = loggingEnabledEdit.Checked;

    private void LogDirectoryBrowse_Click(object? sender, EventArgs e)
    {
        var owner = (Control?)FindForm() ?? this;
        using var waiting = new UiWaitCursor(owner);
        try
        {
            using var dialog = new XtraFolderBrowserDialog
            {
                Title = "Log-Verzeichnis auswählen",
                SelectedPath = logDirectoryEdit.Text,
                StartPosition = FormStartPosition.CenterParent
            };
            if (dialog.ShowDialog(owner) == DialogResult.OK) logDirectoryEdit.Text = dialog.SelectedPath;
        }
        catch (Exception) { ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsValidationFailed); }
    }
    internal async Task SaveAndOfferRestartAsync(Func<bool> confirm, CancellationToken token)
    {
        var previous = _snapshot!.Options.User;
        await SaveAsync(token);
        var saved = _snapshot.Options.User;
        var restartNeeded = RestartSettingsDiffer(saved, _workspace!.Configuration.Current.Options.User);
        if (restartNeeded)
            ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsRestartRequired);
        if (RestartSettingsDiffer(saved, previous) && restartNeeded && confirm())
            RestartRequested?.Invoke(this, EventArgs.Empty);
    }
    private static bool RestartSettingsDiffer(UserSettings first, UserSettings second) =>
        first.ShowWindowHotkey != second.ShowWindowHotkey || first.DialHotkey != second.DialHotkey;

    private async void RefreshCache_Click(object? sender, EventArgs e)
    {
        if (_operation is null || _operation.IsRunning) return;
        await _operation.RunAsync(async stopping =>
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            _cacheCancellation = cancellation;
            SetCacheBusy(true);
            try
            {
                await SaveAsync(cancellation.Token);
                var progress = new Progress<PhoneCacheProgress>(value =>
                {
                    if (IsDisposed || !ReferenceEquals(_cacheCancellation, cancellation) || cancellation.IsCancellationRequested) return;
                    var phase = value.Phase switch { PhoneCacheRefreshPhase.Reading => "Daten lesen", PhoneCacheRefreshPhase.Normalizing => "Rufnummern verarbeiten", _ => "Cache speichern" };
                    ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsCacheUpdating);
                });
                var result = await _workspace!.PhoneCache.RefreshAsync(progress, cancellation.Token);
                foreach (var row in _cacheCompanies)
                    if (result.Companies.FirstOrDefault(company => company.Company == row.Key) is { } company)
                        row.CacheStatus = DescribeCacheResult(company);
                cacheGrid.RefreshDataSource();
                var updated = result.Companies.Count(company => company.State == PhoneCacheRefreshState.Updated);
                ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsCacheUpdated);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsCancelled);
            }
            finally
            {
                _cacheCancellation = null;
                SetCacheBusy(false);
            }
        });
    }

    private void CancelCache_Click(object? sender, EventArgs e) => _cacheCancellation?.Cancel();

    private void SetCacheBusy(bool busy)
    {
        refreshCacheButton.Enabled = loadCompaniesButton.Enabled = saveButton.Enabled = cacheGrid.Enabled = !busy;
        cancelCacheButton.Enabled = busy;
        matchingLayout.Enabled = !busy;
    }

    private static string DescribeCacheResult(PhoneCacheCompanyResult company) => company.State switch
    {
        PhoneCacheRefreshState.Updated => $"{company.PhoneCount} Nummern ({company.UnresolvedPhoneCount} nicht zuordenbar)",
        PhoneCacheRefreshState.NotAuthorized => "Nicht berechtigt oder nicht verfügbar",
        PhoneCacheRefreshState.SelectionChanged => "Einstellungen geändert; bisheriger Cache erhalten",
        _ => company.Failure switch
        {
            PhoneCacheRefreshFailure.Conflict => "Zwischenzeitlich geändert; bitte erneut aktualisieren",
            PhoneCacheRefreshFailure.StorageUnavailable => "Speichern fehlgeschlagen; bisheriger Cache erhalten",
            PhoneCacheRefreshFailure.Unsupported => "Vom Plugin nicht unterstützt",
            _ => "Abruf fehlgeschlagen; bisheriger Cache erhalten"
        }
    };
}
