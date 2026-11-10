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
    private SimpleButton? _dialogCloseButton;
    private bool _arrangingButtons;
    public SettingsPage()
    {
        InitializeComponent();
        transportEdit.Properties.Items.AddRange(new object[] { "UDP", "TCP", "TLS" });
        providerEdit.Properties.Items.AddRange(new object[] { "SQLite", "SQL Server", "MySQL / MariaDB", "PostgreSQL" });
        providerEdit.SelectedIndexChanged += Provider_Changed;
        FontChanged += ButtonLayout_Changed;
        cacheLayout.SizeChanged += ButtonLayout_Changed;
        var appearance = LookAndFeel;
        appearance.StyleChanged += ButtonLayout_Changed;
        Disposed += (_, _) => appearance.StyleChanged -= ButtonLayout_Changed;
        ArrangeButtons();
    }

    internal void ConfigureCommandButtons(SimpleButton closeButton)
    {
        _dialogCloseButton = closeButton;
        ArrangeButtons();
    }
    private void ButtonLayout_Changed(object? sender, EventArgs e) => ArrangeButtons();
    private void ArrangeButtons()
    {
        if (_arrangingButtons || IsDisposed) return;
        _arrangingButtons = true;
        try
        {
            footer.SuspendLayout(); cacheActions.SuspendLayout();
            var commands = _dialogCloseButton is null ? new[] { saveButton, reloadButton } : new[] { saveButton, reloadButton, _dialogCloseButton };
            var commandSize = UiButtonSizing.Apply(commands);
            for (var i = 1; i <= 2; i++)
            {
                footer.ColumnStyles[i].SizeType = SizeType.Absolute;
                footer.ColumnStyles[i].Width = commandSize.Width + saveButton.Margin.Horizontal;
            }
            var cacheButtons = new[] { loadCompaniesButton, refreshCacheButton, cancelCacheButton };
            var cacheSize = UiButtonSizing.Apply(cacheButtons);
            var cellWidth = cacheSize.Width + loadCompaniesButton.Margin.Horizontal;
            var columns = Math.Clamp((cacheLayout.ClientSize.Width - cacheLayout.Padding.Horizontal) / Math.Max(1, cellWidth), 1, 3);
            cacheActions.ColumnCount = columns; cacheActions.RowCount = (3 + columns - 1) / columns;
            cacheActions.ColumnStyles.Clear(); cacheActions.RowStyles.Clear();
            for (var i = 0; i < columns; i++) cacheActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, cellWidth));
            for (var i = 0; i < cacheActions.RowCount; i++) cacheActions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (var i = 0; i < cacheButtons.Length; i++) cacheActions.SetCellPosition(cacheButtons[i], new(i % columns, i / columns));
            footer.ResumeLayout(true); cacheActions.ResumeLayout(true);
        }
        finally { _arrangingButtons = false; }
    }

    internal void Bind(AppWorkspace workspace)
    {
        _workspace = workspace;
        if (workspace.Telephony is not null) workspace.Telephony.RegistrationChanged += SipRegistration_Changed;
        UpdateSipStatus();
        _operation = new PageOperation(this, statusLabel);
        Apply(workspace.Configuration.Current);
        pluginPage.Bind(workspace, _state);
        pluginPage.ConfigureSelection(ActivatePluginAsync);
        pluginPage.RefreshCatalog();
    }
    internal async Task StopAsync()
    {
        if (_operation is not null) await _operation.StopAsync();
        await pluginPage.StopAsync();
    }
    internal void ConfigurePluginSwitch(Func<Task>? beforeSwitch) => _beforePluginSwitch = beforeSwitch;
    internal void SelectPluginsTab() => tabs.SelectedTabPage = pluginTab;
    internal DevExpress.XtraTab.XtraTabControl SettingsTabs => tabs;
    private async Task ActivatePluginAsync(string id, CancellationToken token)
    {
        if (_workspace?.Plugins is not WawiCallPro.Interfaces.Plugins.IProductPluginSelection selection) return;
        saveButton.Enabled = reloadButton.Enabled = false;
        foreach (DevExpress.XtraTab.XtraTabPage tab in tabs.TabPages) tab.PageEnabled = ReferenceEquals(tab, pluginTab);
        try
        {
            pluginPage.ReportSwitchStatus("Offene Ansichten und Vorgänge werden beendet …");
            if (_operation is not null) await _operation.StopAsync();
            if (_beforePluginSwitch is not null) await _beforePluginSwitch();
            await _workspace.Companies.SelectAsync(null);
            pluginPage.ReportSwitchStatus("Bisheriges Plugin wird entladen, neues Plugin wird geladen …");
            await selection.SwitchAsync(id, token);
            pluginPage.ReportSwitchStatus("Plugin ist bereit. Auswahl wird gespeichert …");
            var previous = _snapshot!;
            await _workspace.Settings.SaveAsync(previous.Options with { User = previous.Options.User with { ActivePluginId = id } }, previous, token);
            _snapshot = await _workspace.Settings.LoadAsync(token);
        }
        finally
        {
            _operation?.Dispose(); _operation = new PageOperation(this, statusLabel);
            _cacheCompanies.Clear(); BindCompanyRows();
            saveButton.Enabled = reloadButton.Enabled = true;
            foreach (DevExpress.XtraTab.XtraTabPage tab in tabs.TabPages) tab.PageEnabled = true;
        }
    }
    private void DisposePage()
    {
        _operation?.Dispose();
        if (_workspace?.Telephony is not null) _workspace.Telephony.RegistrationChanged -= SipRegistration_Changed;
        if (!_sipUiLifetime.IsCancellationRequested) { _sipUiLifetime.Cancel(); _sipUiLifetime.Dispose(); }
        _state?.Save(cacheView, "CacheSources");
    }

    internal void ConfigureLayoutPersistence(AppUiState? state)
    {
        _state = state;
        _state?.Restore(cacheView, "CacheSources");
        pluginPage.ConfigureLayoutPersistence(state);
    }

    internal void Apply(ConfigurationSnapshot snapshot)
    {
        _snapshot = snapshot;
        var options = snapshot.Options;
        notificationsEdit.Checked = options.User.ShowCallNotifications;
        minimizedEdit.Checked = options.User.StartMinimized;
        sipEnabledEdit.Checked = options.SipAccount?.RegisterOnStartup == true;
        _sipAdvanced = SipAdvancedSettings.From(options.SipAccount?.Connection ?? new());
        sipProperties.SelectedObject = _sipAdvanced;
        serverEdit.Text = options.SipAccount?.Server ?? "";
        portEdit.Value = options.SipAccount?.Port ?? 5060;
        transportEdit.SelectedIndex = (int)(options.SipAccount?.Transport ?? SipTransport.Udp) - 1;
        usernameEdit.Text = options.SipAccount?.Username ?? "";
        authUserEdit.Text = options.SipAccount?.AuthenticationUsername ?? "";
        passwordEdit.Text = "";
        providerEdit.SelectedIndex = (int)options.Database.Provider - 1;
        sqliteFileEdit.Text = options.Database.SqliteFileName ?? "communications.db";
        connectionEdit.Text = "";
        playVolumeEdit.Value = options.Devices.PlaybackVolumePercent;
        ringVolumeEdit.Value = options.Devices.RingVolumePercent;
        mutedEdit.Checked = options.Devices.MicrophoneMuted;
        var known = _cacheCompanies.Select(row => row.Key).ToHashSet();
        _cacheCompanies.AddRange(options.Companies.Where(company => !known.Contains(company.Company))
            .Select(company => new CacheCompanyRow(company.Company, company.DisplayName ?? "Gespeicherter Mandant", company.IncludeInPhoneCache, false)));
        foreach (var row in _cacheCompanies) row.Selected = options.FindCompanySettings(row.Key)?.IncludeInPhoneCache == true;
        BindCompanyRows();
        statusLabel.Text = snapshot.FileFound ? "Gespeicherte Einstellungen geladen." : "Vorgaben geladen. Es wurden noch keine Einstellungen gespeichert.";
    }

    private void Provider_Changed(object? sender, EventArgs e)
    {
        sqliteFileEdit.Enabled = providerEdit.SelectedIndex == 0;
        connectionEdit.Enabled = providerEdit.SelectedIndex != 0;
        connectionEdit.Text = "";
    }

    internal ApplicationOptions ReadOptions()
    {
        sipProperties.CloseEditor();
        var old = _snapshot!.Options;
        var provider = (CommunicationDatabaseProvider)(providerEdit.SelectedIndex + 1);
        return old with
        {
            User = old.User with { ShowCallNotifications = notificationsEdit.Checked, StartMinimized = minimizedEdit.Checked },
            Devices = old.Devices with { PlaybackVolumePercent = (int)playVolumeEdit.Value, RingVolumePercent = (int)ringVolumeEdit.Value, MicrophoneMuted = mutedEdit.Checked },
            SipAccount = !sipEnabledEdit.Checked && string.IsNullOrWhiteSpace(serverEdit.Text) && string.IsNullOrWhiteSpace(usernameEdit.Text)
                && string.IsNullOrWhiteSpace(authUserEdit.Text) && passwordEdit.Text.Length == 0 ? null : new SipAccountOptions
            {
                RegisterOnStartup = sipEnabledEdit.Checked, Connection = _sipAdvanced.Read(),
                Server = serverEdit.Text.Trim(), Port = (int)portEdit.Value, Transport = (SipTransport)(transportEdit.SelectedIndex + 1),
                Username = usernameEdit.Text.Trim(), AuthenticationUsername = string.IsNullOrWhiteSpace(authUserEdit.Text) ? null : authUserEdit.Text.Trim(),
                Password = passwordEdit.Text.Length == 0 ? old.SipAccount?.Password : _workspace!.Secrets.Protect(passwordEdit.Text, SipAccountOptions.PasswordPurpose)
            },
            Database = new CommunicationDatabaseSettings
            {
                Provider = provider,
                SqliteFileName = provider == CommunicationDatabaseProvider.Sqlite ? sqliteFileEdit.Text.Trim() : null,
                ConnectionString = provider == CommunicationDatabaseProvider.Sqlite ? null : connectionEdit.Text.Length != 0
                    ? _workspace!.Secrets.Protect(connectionEdit.Text, CommunicationDatabaseSettings.ConnectionStringPurpose)
                    : provider == old.Database.Provider ? old.Database.ConnectionString : null
            },
            Companies = ReadCompanySettings(old)
        };
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
            var missing = _cacheCompanies.Where(row => !companies.Any(company => company.Key == row.Key)).ToArray();
            _cacheCompanies = companies.Select(company => new CacheCompanyRow(company.Key, company.DisplayName,
                selection.TryGetValue(company.Key, out var selected) ? selected : _snapshot!.Options.FindCompanySettings(company.Key)?.IncludeInPhoneCache == true)).Concat(missing).ToList();
            BindCompanyRows();
            statusLabel.Text = companies.IsEmpty ? "Keine freigegebenen Mandanten gefunden. Gespeicherte Zuordnungen bleiben erhalten." : "Mandanten für den Telefon-Cache auswählen und Einstellungen speichern.";
        });
    }

    private void BindCompanyRows()
    {
        cacheGrid.DataSource = _cacheCompanies;
        cacheGrid.RefreshDataSource();
    }

    internal Task ReloadAsync() => _operation?.RunAsync(async token => Apply(await _workspace!.Settings.LoadAsync(token))) ?? Task.CompletedTask;

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
        var options = ReadOptions();
        await _workspace!.Settings.SaveAsync(options, _snapshot!, token);
        Apply(await _workspace.Settings.LoadAsync(token));
        statusLabel.Text = "Einstellungen gespeichert. Konto, Datenbank und Geräte werden beim nächsten Programmstart übernommen.";
    }
    private async void Save_Click(object? sender, EventArgs e)
    {
        if (_operation is not null) await _operation.RunAsync(SaveAsync);
    }
    private async void Reload_Click(object? sender, EventArgs e)
    {
        await ReloadAsync();
    }

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
                    statusLabel.Text = $"{value.CompanyNumber}/{value.CompanyCount}: {value.CompanyName} – {phase} ({value.PartyCount} Geschäftspartner)";
                });
                var result = await _workspace!.PhoneCache.RefreshAsync(progress, cancellation.Token);
                foreach (var row in _cacheCompanies)
                    if (result.Companies.FirstOrDefault(company => company.Company == row.Key) is { } company)
                        row.CacheStatus = DescribeCacheResult(company);
                cacheGrid.RefreshDataSource();
                var updated = result.Companies.Count(company => company.State == PhoneCacheRefreshState.Updated);
                statusLabel.Text = result.Companies.IsEmpty ? "Keine Cache-Mandanten ausgewählt."
                    : $"Cache aktualisiert: {updated} von {result.Companies.Length} Mandanten. Einzelheiten stehen in der Mandantenliste.";
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                statusLabel.Text = "Cacheaktualisierung abgebrochen. Bereits abgeschlossene Mandanten bleiben gespeichert; der laufende Mandant behält seinen bisherigen Cache.";
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
        refreshCacheButton.Enabled = loadCompaniesButton.Enabled = saveButton.Enabled = reloadButton.Enabled = cacheGrid.Enabled = !busy;
        cancelCacheButton.Enabled = busy;
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
