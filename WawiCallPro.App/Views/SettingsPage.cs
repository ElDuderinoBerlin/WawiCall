using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Configuration;
using System.Collections.Immutable;
using System.ComponentModel;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.App.Views;

public partial class SettingsPage : XtraUserControl
{
    private AppWorkspace? _workspace;
    private ConfigurationSnapshot? _snapshot;
    private PageOperation? _operation;
    private List<CacheCompanyRow> _cacheCompanies = [];
    public SettingsPage()
    {
        InitializeComponent();
        transportEdit.Properties.Items.AddRange(new object[] { "UDP", "TCP", "TLS" });
        providerEdit.Properties.Items.AddRange(new object[] { "SQLite", "SQL Server", "MySQL / MariaDB", "PostgreSQL" });
        providerEdit.SelectedIndexChanged += Provider_Changed;
    }

    internal void Bind(AppWorkspace workspace)
    {
        _workspace = workspace;
        _operation = new PageOperation(this, statusLabel);
        Apply(workspace.Configuration.Current);
    }
    internal Task StopAsync() => _operation?.StopAsync() ?? Task.CompletedTask;
    private void DisposePage() => _operation?.Dispose();

    internal void Apply(ConfigurationSnapshot snapshot)
    {
        _snapshot = snapshot;
        var options = snapshot.Options;
        notificationsEdit.Checked = options.User.ShowCallNotifications;
        minimizedEdit.Checked = options.User.StartMinimized;
        sipEnabledEdit.Checked = options.SipAccount is not null;
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
        var old = _snapshot!.Options;
        var provider = (CommunicationDatabaseProvider)(providerEdit.SelectedIndex + 1);
        return old with
        {
            User = old.User with { ShowCallNotifications = notificationsEdit.Checked, StartMinimized = minimizedEdit.Checked },
            Devices = old.Devices with { PlaybackVolumePercent = (int)playVolumeEdit.Value, RingVolumePercent = (int)ringVolumeEdit.Value, MicrophoneMuted = mutedEdit.Checked },
            SipAccount = !sipEnabledEdit.Checked ? null : new SipAccountOptions
            {
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
}
