using System.Collections.Immutable;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Views;

public partial class MailSettingsPage : XtraUserControl
{
    private AppWorkspace? _workspace;
    private Func<CancellationToken, Task>? _save;
    private Func<CancellationToken, Task>? _reload;
    private List<MailAccountSettings> _accounts = [];
    private readonly Dictionary<Guid, ImmutableArray<MailFolder>> _folders = [];
    private Guid? _selected;
    private bool _binding;
    private Task _running = Task.CompletedTask;
    private CancellationTokenSource? _request;
    private bool _stopping;
    private AppUiState? _state;
    private bool _arranging;
    public MailSettingsPage()
    {
        InitializeComponent(); UiButtonSizing.Attach(this);
        providerEdit.Properties.Items.AddRange(new object[] { "Outlook Desktop (klassisch)", "Microsoft 365", "Gmail / Google Workspace" });
        companyEdit.Properties.Columns.Add(new LookUpColumnInfo("Name"));
        companyEdit.Properties.DisplayMember = "Name"; companyEdit.Properties.ValueMember = "Key";
        storeEdit.Properties.Columns.Add(new LookUpColumnInfo("Name"));
        storeEdit.Properties.DisplayMember = "Name"; storeEdit.Properties.ValueMember = "Id";
        companyEdit.Properties.SearchMode = storeEdit.Properties.SearchMode = SearchMode.AutoSearch;
        FontChanged += Appearance_Changed; SizeChanged += Appearance_Changed;
        connectionLayout.Layout += ConnectionLayout_Changed;
        Appearance_Changed(this, EventArgs.Empty);
    }
    internal void Bind(AppWorkspace workspace, Func<CancellationToken, Task> save, Func<CancellationToken, Task> reload)
    { _workspace = workspace; _save = save; _reload = reload; }
    internal void ConfigureLayoutPersistence(AppUiState? state)
    { _state = state; state?.Restore(accountsView, "MailAccounts-v1"); state?.Restore(foldersView, "MailImportFolders-v1"); }
    internal void Apply(ImmutableArray<MailAccountSettings> accounts, ImmutableArray<CompanySettings> companies)
    {
        _accounts = accounts.ToList();
        companyEdit.Properties.DataSource = companies.Select(item => new CompanyRow(item.Company, item.DisplayName ?? "Gespeicherter Mandant")).ToArray();
        BindAccounts(_selected);
    }
    internal ImmutableArray<MailAccountSettings> ReadAccounts() { SaveDraft(); return [.. _accounts]; }
    internal void ApplyCache(MailCacheSettings policy)
    { cacheEnabledEdit.Checked = policy.Enabled; cacheDaysEdit.Value = policy.Days; CacheEnabled_Changed(this, EventArgs.Empty); }
    internal MailCacheSettings ReadCache() => new() { Enabled = cacheEnabledEdit.Checked, Days = (int)cacheDaysEdit.Value };
    private void CacheEnabled_Changed(object? sender, EventArgs e)
    {
        cacheDaysEdit.Enabled = cacheEnabledEdit.Checked;
        syncButton.Enabled = cacheEnabledEdit.Checked && _selected is not null && _request is null && !_stopping;
    }
    internal int GetRequiredHeight()
    {
        var width = Math.Max(1, Width - layout.Padding.Horizontal);
        return header.GetPreferredSize(new(width, 0)).Height + header.Margin.Vertical + Font.Height * 15
            + cacheLayout.GetPreferredSize(new(width, 0)).Height + cacheLayout.Margin.Vertical
            + actions.GetPreferredSize(new(width, 0)).Height + actions.Margin.Vertical
            + statusLabel.GetPreferredSize(new(width, 0)).Height + statusLabel.Margin.Vertical + layout.Padding.Vertical;
    }
    private void BindAccounts(Guid? selected)
    {
        _binding = true;
        try
        {
            accountsGrid.DataSource = _accounts.Select(item => new AccountRow(item.Id, item.DisplayName, item.Enabled ? "Aktiv" : "Entwurf / getrennt")).ToArray();
            var index = _accounts.FindIndex(item => item.Id == selected); if (index < 0) index = _accounts.Count == 0 ? -1 : 0;
            accountsView.FocusedRowHandle = index; _selected = index < 0 ? null : _accounts[index].Id;
            ShowDraft();
        }
        finally { _binding = false; if (_request is not null) SetBusy(true); }
    }
    private void SaveDraft()
    {
        if (_binding || _selected is not { } id || _workspace is null) return;
        var old = _accounts.Single(item => item.Id == id);
        var selected = foldersView.GetSelectedRows().Select(handle => foldersView.GetRow(handle)).OfType<FolderRow>().Select(item => item.Id).ToImmutableArray();
        var names = _folders.TryGetValue(id, out var catalog) ? catalog.Where(item => selected.Contains(item.Key.Id)).ToImmutableDictionary(item => item.Key.Id, item => item.Name) : old.FolderNames.Where(pair => selected.Contains(pair.Key)).ToImmutableDictionary();
        var provider = (MailProviderKind)(providerEdit.SelectedIndex + 1);
        _accounts[_accounts.FindIndex(item => item.Id == id)] = old with
        {
            Provider = provider, DisplayName = nameEdit.Text.Trim(), Enabled = enabledEdit.Checked,
            ClientId = string.IsNullOrWhiteSpace(clientEdit.Text) ? null : clientEdit.Text.Trim(), Tenant = tenantEdit.Text.Trim(),
            ApplicationAuthentication = applicationEdit.Checked, Mailbox = string.IsNullOrWhiteSpace(mailboxEdit.Text) ? null : mailboxEdit.Text.Trim(),
            MicrosoftClientSecret = secretEdit.Text.Length == 0 ? old.MicrosoftClientSecret : _workspace.Secrets.Protect(secretEdit.Text, MailAccountSettings.MicrosoftSecretPurpose),
            OutlookStoreId = storeEdit.EditValue as string is { Length: > 0 } store ? store : null,
            Company = companyEdit.EditValue as CompanyKey, FolderIds = selected, FolderNames = names,
            ImportDays = (int)daysEdit.Value, MaximumMessages = (int)maximumEdit.Value, SynchronizeOnStartup = startupEdit.Checked
        };
        secretEdit.Text = "";
    }
    private void ShowDraft()
    {
        var account = _accounts.SingleOrDefault(item => item.Id == _selected);
        detailsTabs.Enabled = account is not null; connectButton.Enabled = disconnectButton.Enabled = syncButton.Enabled = loadFoldersButton.Enabled = removeButton.Enabled = account is not null && _running.IsCompleted;
        if (account is null) { foldersGrid.DataSource = Array.Empty<FolderRow>(); return; }
        nameEdit.Text = account.DisplayName; enabledEdit.Checked = account.Enabled; providerEdit.SelectedIndex = (int)account.Provider - 1;
        clientEdit.Text = account.ClientId ?? ""; tenantEdit.Text = account.Tenant; mailboxEdit.Text = account.Mailbox ?? "";
        applicationEdit.Checked = account.ApplicationAuthentication; secretEdit.Text = "";
        storeEdit.Properties.DataSource = new[] { new MailStore(account.OutlookStoreId ?? "", account.OutlookStoreId is null ? "Alle Outlook-Postfächer" : "Gespeichertes Outlook-Postfach") };
        storeEdit.EditValue = account.OutlookStoreId ?? ""; companyEdit.EditValue = account.Company;
        daysEdit.Value = account.ImportDays; maximumEdit.Value = account.MaximumMessages; startupEdit.Checked = account.SynchronizeOnStartup;
        var folders = _folders.TryGetValue(account.Id, out var catalog) ? catalog : account.FolderIds.Select(id => new MailFolder(new("", "", id), null, account.FolderNames.GetValueOrDefault(id, "Gespeicherter Ordner"))).ToImmutableArray();
        foldersGrid.DataSource = folders.Select(item => new FolderRow(item.Key.Id, item.Name)).ToArray(); foldersView.ClearSelection();
        for (var index = 0; index < foldersView.DataRowCount; index++) if (foldersView.GetRow(index) is FolderRow row && account.FolderIds.Contains(row.Id)) foldersView.SelectRow(index);
        ShowProvider();
        CacheEnabled_Changed(this, EventArgs.Empty);
    }
    private void Accounts_Changed(object? sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
    {
        if (_binding || !_running.IsCompleted) return;
        SaveDraft(); _selected = (accountsView.GetFocusedRow() as AccountRow)?.Id;
        _binding = true; try { ShowDraft(); } finally { _binding = false; }
    }
    private void Provider_Changed(object? sender, EventArgs e)
    {
        if (_binding) return;
        if (_selected is { } id && _accounts.Single(item => item.Id == id).Provider != (MailProviderKind)(providerEdit.SelectedIndex + 1))
        {
            var old = _accounts.Single(item => item.Id == id);
            if (old.RemoteIdentity is not null || old.FolderIds.Length > 0)
            { providerEdit.SelectedIndex = (int)old.Provider - 1; statusLabel.Text = "Für einen anderen Anbieter ein neues Postfach anlegen."; return; }
        }
        ShowProvider();
    }
    private void ShowProvider()
    {
        var outlook = providerEdit.SelectedIndex == 0; var microsoft = providerEdit.SelectedIndex == 1;
        clientLabel.Visible = clientEdit.Visible = !outlook;
        tenantLabel.Visible = tenantEdit.Visible = applicationEdit.Visible = microsoft;
        mailboxLabel.Visible = mailboxEdit.Visible = secretLabel.Visible = secretEdit.Visible = microsoft && applicationEdit.Checked;
        storeLabel.Visible = storeEdit.Visible = storesButton.Visible = outlook;
        googleButton.Visible = providerEdit.SelectedIndex == 2;
        hintLabel.Text = outlook ? "Klassisches Outlook mit eingerichtetem Profil erforderlich. Es werden keine Nachrichten geändert und Outlook wird nicht beendet." : microsoft ?
            applicationEdit.Checked ? "App-Anmeldung: Mail.Read als Anwendungsberechtigung mit Administratorzustimmung. Vorhandene Mail.Send-Rechte genügen nicht. Secret bleibt lokal geschützt." :
            "Desktop-App in Microsoft Entra registrieren: öffentlicher Client, Umleitung http://localhost, delegierte Rechte Mail.Read und User.Read. Anmeldung öffnet Ihren Browser." :
            "Google Cloud: Gmail API aktivieren, OAuth-Zustimmungsbildschirm und Desktop-Client anlegen. Client-JSON importieren; Leserecht gmail.readonly. Im Testbetrieb das Konto als Testnutzer zulassen.";
        hintLabel.Text += "\nZeitraum: 0 umfasst alle Nachrichten. Die Mengenbegrenzung gilt je Ordner.";
    }
    private void New_Click(object? sender, EventArgs e)
    {
        SaveDraft(); var account = new MailAccountSettings { Id = Guid.NewGuid(), DisplayName = "Neues Postfach", Enabled = false };
        _accounts.Add(account); BindAccounts(account.Id); statusLabel.Text = "Anbieter wählen, Verbindung einrichten und anschließend Ordner auswählen.";
    }
    private void Remove_Click(object? sender, EventArgs e) => Start(async token =>
    {
        if (_selected is not { } id) return;
        await _workspace!.Mail.Accounts.ForgetAuthenticationAsync(_accounts.Single(item => item.Id == id), token);
        _accounts.RemoveAll(item => item.Id == id); _folders.Remove(id); BindAccounts(null);
        await _save!(token); statusLabel.Text = "Postfach und gespeicherte Anmeldung entfernt. Importierte Inhalte bleiben geschützt in der Datenbank.";
    });
    private void Connect_Click(object? sender, EventArgs e) => Start(async token =>
    {
        var id = _selected!.Value; await _save!(token); await _workspace!.Mail.Accounts.ConnectAsync(id, token); await _reload!(token);
        await LoadFolders(id, token); statusLabel.Text = "Verbindung und Leserechte geprüft. Ordner auswählen und Einstellungen speichern.";
    });
    private void Disconnect_Click(object? sender, EventArgs e) => Start(async token =>
    { var id = _selected!.Value; await _save!(token); await _workspace!.Mail.Accounts.DisconnectAsync(id, token); await _reload!(token); statusLabel.Text = "Postfach getrennt, gespeicherte Anmeldung entfernt."; });
    private void LoadFolders_Click(object? sender, EventArgs e) => Start(async token =>
    { var id = _selected!.Value; await _save!(token); await LoadFolders(id, token); statusLabel.Text = "Zu importierende Ordner über die Checkboxen auswählen. Danach speichern."; detailsTabs.SelectedTabPage = foldersTab; });
    private async Task LoadFolders(Guid id, CancellationToken token)
    {
        var account = await _workspace!.Mail.Accounts.GetAsync(id, token); var folders = await _workspace.Mail.Accounts.GetFoldersAsync(account, token);
        _folders[id] = folders.Folders; _binding = true; try { ShowDraft(); } finally { _binding = false; }
    }
    private void Sync_Click(object? sender, EventArgs e) => Start(async token =>
    {
        var id = _selected!.Value; await _save!(token);
        if (!await _workspace!.Mail.Cache.IsEnabledAsync(token)) { statusLabel.Text = "Mailcache ist ausgeschaltet. Nachrichten werden in der Mailansicht direkt geladen."; return; }
        var progress = new Progress<WawiCallPro.Office.MailSyncProgress>(value => { if (!_stopping && !IsDisposed) statusLabel.Text = value.FoldersCompleted + "/" + value.FoldersTotal + " Ordner · " + value.MessagesConfirmed + " Nachrichten bestätigt"; });
        await _workspace!.Mail.Synchronization.SynchronizeAsync(id, progress, token); statusLabel.Text = "Import abgeschlossen. In der Mailansicht Aktualisieren wählen.";
    });
    private void Stores_Click(object? sender, EventArgs e) => Start(async token =>
    {
        SaveDraft(); var account = _accounts.Single(item => item.Id == _selected);
        var stores = await _workspace!.Mail.Accounts.GetOutlookStoresAsync(account, token);
        storeEdit.Properties.DataSource = new[] { new MailStore("", "Alle Outlook-Postfächer") }.Concat(stores).ToArray();
        storeEdit.EditValue = account.OutlookStoreId ?? ""; statusLabel.Text = "Outlook-Postfach auswählen. Anschließend verbinden und Ordner laden.";
    });
    private void Companies_Click(object? sender, EventArgs e) => Start(async token =>
    {
        if (_workspace?.CanUseBusinessDirectory != true) { statusLabel.Text = "Lexware-Zugriff ist gesperrt. Details stehen im Log-Fenster."; return; }
        var selection = companyEdit.EditValue; var companies = await _workspace.Directory.GetCompaniesAsync(token);
        companyEdit.Properties.DataSource = companies.Select(item => new CompanyRow(item.Key, item.DisplayName)).ToArray(); companyEdit.EditValue = selection;
        statusLabel.Text = "Mandant für die Anzeige von Kundenmails bei Anrufen auswählen.";
    });
    private void Google_Click(object? sender, EventArgs e) => Browse();

    private void Browse() => Start(async token =>
    {
        await Task.Yield(); var owner = (Control?)FindForm() ?? this;
        using var waiting = new UiWaitCursor(owner);
        using var dialog = new XtraOpenFileDialog { Title = "Google Desktop-Client importieren", Filter = "JSON-Dateien (*.json)|*.json", Multiselect = false, CheckFileExists = true, RestoreDirectory = true };
        if (dialog.ShowDialog(owner) != DialogResult.OK) { statusLabel.Text = "Import abgebrochen."; return; }
        SaveDraft();
        var imported = new[] { await _workspace!.Mail.Accounts.ReadGoogleClientAsync(dialog.FileName, token) };
        Guid? selected = null;
        foreach (var account in imported)
        {
            if (_accounts.Any(item => item.Id == account.Id)) { selected = account.Id; continue; }
            _accounts.Add(account with { Enabled = false }); selected = account.Id;
        }
        BindAccounts(selected); statusLabel.Text = "Clientdaten geschützt übernommen. Verbinden prüft die Leserechte. Es wurden noch keine Mails gelesen.";
    });
    private void Start(Func<CancellationToken, Task> operation)
    { if (_workspace is not null && !_stopping && _running.IsCompleted) _running = Run(operation); }
    private async Task Run(Func<CancellationToken, Task> operation)
    {
        using var waiting = new UiWaitCursor(this);
        using var request = new CancellationTokenSource(); _request = request;
        SetBusy(true); statusLabel.Text = "Bitte warten …";
        try { await Task.Yield(); await operation(request.Token); }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { if (!_stopping) statusLabel.Text = "Vorgang abgebrochen. Bereits bestätigte Importseiten bleiben erhalten."; }
        catch (Exception exception) { if (!_stopping) statusLabel.Text = PageOperation.Describe(exception); }
        finally { _request = null; if (!IsDisposed && !_stopping) SetBusy(false); }
    }
    private void SetBusy(bool busy)
    {
        accountsGrid.Enabled = newButton.Enabled = !busy;
        detailsTabs.Enabled = removeButton.Enabled = !busy && _selected is not null;
        connectButton.Enabled = disconnectButton.Enabled = syncButton.Enabled = loadFoldersButton.Enabled = !busy && _selected is not null;
        syncButton.Enabled = syncButton.Enabled && cacheEnabledEdit.Checked;
        cacheLayout.Enabled = !busy;
        cancelButton.Enabled = busy;
    }
    private void Cancel_Click(object? sender, EventArgs e) => _request?.Cancel();
    internal async Task StopAsync() { _stopping = true; _request?.Cancel(); await _running; }
    private void DisposePage()
    { _stopping = true; _request?.Cancel(); _state?.Save(accountsView, "MailAccounts-v1"); _state?.Save(foldersView, "MailImportFolders-v1"); }
    private void Appearance_Changed(object? sender, EventArgs e)
    {
        if (_arranging || IsDisposed) return;
        _arranging = true;
        try
        {
            cacheDaysEdit.MinimumSize = new(cacheDaysEdit.LogicalToDeviceUnits(110), 0);
            UiButtonSizing.Apply(newButton, removeButton);
            UiButtonSizing.Apply(googleButton); UiButtonSizing.Apply(storesButton); UiButtonSizing.Apply(companiesButton); UiButtonSizing.Apply(loadFoldersButton);
            UiButtonSizing.Apply(connectButton, syncButton);
            UiButtonSizing.Apply(disconnectButton, cancelButton);
            var available = Math.Max(1, ClientSize.Width - layout.Padding.Horizontal);
            foreach (var (table, buttons) in new[] { (header, new[] { newButton, removeButton }), (actions, new[] { connectButton, disconnectButton, syncButton, cancelButton }) })
            {
                foreach (var button in buttons) button.Margin = new Padding(LogicalToDeviceUnits(4));
                var columns = buttons.Length;
                while (columns > 1 && Enumerable.Range(0, columns).Sum(column => buttons.Where((_, index) => index % columns == column)
                    .Max(button => button.Width + button.Margin.Horizontal)) > available) columns--;
                table.SuspendLayout(); table.ColumnCount = columns; table.RowCount = (buttons.Length + columns - 1) / columns;
                table.ColumnStyles.Clear(); table.RowStyles.Clear();
                for (var i = 0; i < columns; i++) table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                for (var i = 0; i < table.RowCount; i++) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                for (var i = 0; i < buttons.Length; i++) table.SetCellPosition(buttons[i], new(i % columns, i / columns));
                table.ResumeLayout(true);
            }
            var cacheWidth = cacheEnabledEdit.PreferredSize.Width + cacheEnabledEdit.Margin.Horizontal
                + cacheDaysLabel.PreferredSize.Width + cacheDaysLabel.Margin.Horizontal + cacheDaysEdit.MinimumSize.Width + cacheDaysEdit.Margin.Horizontal;
            var wrapCache = cacheWidth > available;
            cacheLayout.SuspendLayout(); cacheLayout.ColumnCount = wrapCache ? 2 : 3; cacheLayout.RowCount = wrapCache ? 2 : 1;
            cacheLayout.ColumnStyles.Clear(); cacheLayout.RowStyles.Clear();
            cacheLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            cacheLayout.ColumnStyles.Add(new ColumnStyle(wrapCache ? SizeType.Percent : SizeType.AutoSize, wrapCache ? 100F : 0F));
            if (!wrapCache) cacheLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            cacheLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (wrapCache) cacheLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cacheLayout.SetColumnSpan(cacheEnabledEdit, wrapCache ? 2 : 1);
            cacheLayout.SetCellPosition(cacheDaysLabel, wrapCache ? new(0, 1) : new(1, 0));
            cacheLayout.SetCellPosition(cacheDaysEdit, wrapCache ? new(1, 1) : new(2, 0));
            cacheLayout.ResumeLayout(true);
            var hintWidth = Math.Max(1, connectionLayout.ClientSize.Width - connectionLayout.Padding.Horizontal - hintLabel.Margin.Horizontal);
            var hintHeight = hintLabel.GetPreferredSize(new(hintWidth, 0)).Height;
            if (hintLabel.MinimumSize.Height != hintHeight) hintLabel.MinimumSize = new(0, hintHeight);
            var scrollSize = new Size(0, connectionLayout.Height + connectionLayout.Margin.Vertical);
            if (connectionScroll.AutoScrollMinSize != scrollSize) connectionScroll.AutoScrollMinSize = scrollSize;
            MinimumSize = new Size(0, GetRequiredHeight());
        }
        finally { _arranging = false; }
    }
    private void ConnectionLayout_Changed(object? sender, LayoutEventArgs e) => Appearance_Changed(sender, e);
    private sealed record AccountRow(Guid Id, string Name, string Status);
    private sealed record FolderRow(string Id, string Name);
    private sealed record CompanyRow(CompanyKey Key, string Name);
}
