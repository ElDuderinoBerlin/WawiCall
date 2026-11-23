using System.Collections.Immutable;
using System.ComponentModel;
using DevExpress.XtraEditors;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;

namespace WawiCallPro.Plugin.Windows;

/// <summary>Plugin-owned multi-company draft. Loading never publishes a live selection.</summary>
public partial class PluginCompanySettingsPage : XtraUserControl
{
    private IPluginSettingsPageContext? _context;
    private IBusinessDirectory? _directory;
    private LexwareProductLine _productLine;
    private Func<bool>? _connectionReady;
    private readonly CancellationTokenSource _stopping = new();
    private Task _loading = Task.CompletedTask;
    private ImmutableArray<Company> _companies = [];
    private CompanyKey[] _savedScope = [];
    private CompanyKey? _savedSelection;
    private bool _binding, _sizing, _stopped, _disposed, _layoutRestored;
    private int _revision;
    public PluginCompanySettingsPage() => InitializeComponent();
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (layout is null) return base.GetPreferredSize(proposedSize);
        var width = Math.Max(1, proposedSize.Width > 0 ? proposedSize.Width : Width);
        return new(width, layout.GetPreferredSize(new(width, 0)).Height + Padding.Vertical);
    }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasUnsavedChanges => _context is not null &&
        (!_savedScope.ToHashSet().SetEquals(_context.Companies.SelectedCompanies.Select(item => item.Key)) || _savedSelection != _context.Companies.SelectedCompany);
    public void Bind(IPluginSettingsPageContext? context, IBusinessDirectory directory, LexwareProductLine line,
        Func<bool> connectionReady, Func<bool>? connectionChanged = null, Func<Exception, string>? describeFailure = null)
    {
        _context = context; _directory = directory; _productLine = line; _connectionReady = connectionReady;
        _savedScope = context?.Companies.SelectedCompanies.Select(item => item.Key).ToArray() ?? [];
        _savedSelection = context?.Companies.SelectedCompany; _revision++;
        if (!_layoutRestored && context is not null) { context.Presentation.RestoreCompanyLayout(companyView); _layoutRestored = true; }
        BindRows();
        refreshButton.Enabled = clearButton.Enabled = context is not null && !_stopped;
        context?.Presentation.SizeButtons(refreshButton, clearButton);
    }
    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_stopped || !_loading.IsCompleted) return _loading;
        return _loading = LoadCoreAsync(cancellationToken);
    }
    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        var context = _context; var directory = _directory; var revision = _revision;
        if (context is null || directory is null) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);
        var token = linked.Token;
        using var waiting = context.Presentation.BeginWait(this);
        refreshButton.Enabled = companyGrid.Enabled = clearButton.Enabled = false;
        context.Presentation.Report(PluginSettingsNotice.Loading);
        var stage = PluginCompanyFailure.ConfigurationRequired;
        try
        {
            if (_connectionReady?.Invoke() != true) throw new InvalidOperationException("Connection not configured.");
            stage = PluginCompanyFailure.SourceAccess;
            if (context is IPluginSettingsSourceAccess access) await access.EnsureSourceAccessAsync(token);
            stage = PluginCompanyFailure.Unknown;
            var companies = await directory.GetCompaniesAsync(token);
            token.ThrowIfCancellationRequested();
            if (_stopped || revision != _revision) return;
            stage = PluginCompanyFailure.SourceAccess;
            if (context is IPluginSettingsSourceAccess current) await current.EnsureSourceAccessAsync(token);
            stage = PluginCompanyFailure.InvalidResponse;
            CheckCatalog(companies);
            _companies = companies.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToImmutableArray();
            BindRows();
            context.Presentation.Report(_companies.IsEmpty ? PluginSettingsNotice.Empty
                : context.Companies.SelectedCompanies.Any(selected => !_companies.Any(item => item.Key == selected.Key))
                    ? PluginSettingsNotice.Unavailable : PluginSettingsNotice.Loaded);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (_stopped || revision != _revision) return;
            _companies = []; BindRows();
            var failure = error is IPluginCompanyFailure source ? source.CompanyFailure
                : error is WawiCallPro.Interfaces.Plugins.PluginSettingsValidationException ? PluginCompanyFailure.ConfigurationRequired : stage;
            context.Presentation.ReportCompanyFailure(Enum.IsDefined(failure) ? failure : PluginCompanyFailure.Unknown);
        }
        finally
        {
            if (!_stopped && revision == _revision)
            { refreshButton.Enabled = clearButton.Enabled = true; companyGrid.Enabled = !_companies.IsEmpty; }
        }
    }
    private void CheckCatalog(ImmutableArray<Company> companies)
    {
        if (companies.IsDefault || companies.Length > 1000 || companies.Any(item => item?.Key is null || item.Key.ProductLine != _productLine
            || string.IsNullOrWhiteSpace(item.DisplayName) || item.DisplayName.Length > 256 || item.DisplayName.Any(char.IsControl))
            || companies.Select(item => item.Key).Distinct().Count() != companies.Length) throw new InvalidOperationException("Invalid company catalog.");
    }
    private void BindRows()
    {
        if (_context is null) return;
        _binding = true;
        try
        {
            var selected = _context.Companies.SelectedCompanies.ToDictionary(item => item.Key);
            var rows = _companies.Select(item => new CompanyRow(item, selected.ContainsKey(item.Key), true))
                .Concat(selected.Values.Where(item => !_companies.Any(source => source.Key == item.Key)).Select(item => new CompanyRow(item, true, false))).ToArray();
            companyGrid.DataSource = rows; companyGrid.RefreshDataSource();
            companyView.ClearSelection();
            for (var i = 0; i < companyView.DataRowCount; i++) if (companyView.GetRow(i) is CompanyRow row && selected.ContainsKey(row.Company.Key)) companyView.SelectRow(i);
            CompanySize_Changed(this, EventArgs.Empty);
        }
        finally { _binding = false; }
    }
    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        await _loading.WaitAsync(cancellationToken);
        if (!HasUnsavedChanges) return;
        await ValidateCurrentScopeAsync(cancellationToken);
    }
    public async Task ValidateCurrentScopeAsync(CancellationToken cancellationToken = default)
    {
        if (_context is null || _directory is null) return;
        var selected = _context.Companies.SelectedCompanies.Select(item => item.Key).ToArray();
        var preferredChanged = _context.Companies.SelectedCompany != _savedSelection;
        if (selected.Length == 0 && !preferredChanged) return;
        if (_context is IPluginSettingsSourceAccess access) await access.EnsureSourceAccessAsync(cancellationToken);
        var catalog = await _directory.GetCompaniesAsync(cancellationToken);
        CheckCatalog(catalog); cancellationToken.ThrowIfCancellationRequested();
        if (_context is IPluginSettingsSourceAccess current) await current.EnsureSourceAccessAsync(cancellationToken);
        if (selected.Any(key => !catalog.Any(item => item.Key == key)) || preferredChanged && _context.Companies.SelectedCompany is { } preferred && !catalog.Any(item => item.Key == preferred))
            throw new InvalidOperationException("Selected company is not in the authorized catalog.");
    }
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;
        if (!_stopped) _context?.Presentation.SaveCompanyLayout(companyView);
        _stopped = true; await _stopping.CancelAsync(); await _loading;
    }
    private async void Refresh_Click(object? sender, EventArgs e) => await LoadAsync();
    private void Clear_Click(object? sender, EventArgs e)
    {
        if (_context is null || _stopped) return;
        _context.Companies.StageSelections(Array.Empty<Company>()); BindRows();
        _context.Presentation.Report(PluginSettingsNotice.SelectionChanged);
    }
    private void Company_Changed(object? sender, DevExpress.Data.SelectionChangedEventArgs e)
    {
        if (_binding || _stopped || _context is null) return;
        var selected = companyView.GetSelectedRows().Select(handle => companyView.GetRow(handle)).OfType<CompanyRow>().ToArray();
        if (selected.Any(item => !item.Available && !_savedScope.Contains(item.Company.Key))) return;
        _context.Companies.StageSelections(selected.Select(item => item.Company).ToArray());
        _context.Presentation.Report(PluginSettingsNotice.SelectionChanged);
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F))
        { if (companyView.IsFindPanelVisible) companyView.HideFindPanel(); else companyView.ShowFindPanel(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    private void CompanySize_Changed(object? sender, EventArgs e)
    {
        if (_sizing || _stopped) return;
        _sizing = true;
        try
        {
            _context?.Presentation.SizeButtons(refreshButton, clearButton);
            companyView.OptionsSelection.CheckBoxSelectorColumnWidth = LogicalToDeviceUnits(32);
            companyGrid.Height = (Math.Clamp(companyView.DataRowCount, 2, 6) + 2) * (Font.Height + LogicalToDeviceUnits(8));
        }
        finally { _sizing = false; }
    }
    private sealed class CompanyRow(Company company, bool selected, bool available)
    {
        public Company Company { get; } = company;
        public bool Selected { get; set; } = selected;
        public bool Available { get; } = available;
        public string Name => Available ? Company.DisplayName : Company.DisplayName + " (nicht verfügbar)";
    }
    private void DisposePage()
    {
        if (_disposed) return;
        _disposed = _stopped = true; _stopping.Cancel(); _stopping.Dispose(); _context = null; _directory = null;
    }
}
