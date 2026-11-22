using System.Collections.Immutable;
using System.ComponentModel;
using DevExpress.XtraEditors;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;

namespace WawiCallPro.Plugin.Windows;

/// <summary>Shared plugin UI for a dialog-local selection. Product directories and error translation are supplied by the plugin.</summary>
public partial class PluginCompanySettingsPage : XtraUserControl
{
    private IPluginSettingsPageContext? _context;
    private IBusinessDirectory? _directory;
    private LexwareProductLine _productLine;
    private Func<bool>? _connectionReady;
    private Func<bool>? _connectionChanged;
    private Func<Exception, string>? _describeFailure;
    private ImmutableArray<Company> _companies = [];
    private CompanyKey? _savedSelection;
    private readonly CancellationTokenSource _stopping = new();
    private Task _loading = Task.CompletedTask;
    private bool _binding, _sizing, _sizingButtons, _stopped, _disposed;
    private int _revision;

    public PluginCompanySettingsPage() => InitializeComponent();
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (layout is null) return base.GetPreferredSize(proposedSize);
        var width = Math.Max(1, proposedSize.Width > 0 ? proposedSize.Width : Width);
        return new(width, layout.GetPreferredSize(new(width, 0)).Height + Padding.Vertical);
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasUnsavedChanges => _context?.Companies.SelectedCompany != _savedSelection;

    public void Bind(IPluginSettingsPageContext? context, IBusinessDirectory directory, LexwareProductLine line,
        Func<bool> connectionReady, Func<bool>? connectionChanged = null, Func<Exception, string>? describeFailure = null)
    {
        _context = context; _directory = directory; _productLine = line;
        _connectionReady = connectionReady; _connectionChanged = connectionChanged; _describeFailure = describeFailure;
        _savedSelection = context?.Companies.SelectedCompany;
        _revision++;
        _binding = true;
        try { companyEdit.EditValue = _savedSelection; }
        finally { _binding = false; }
        refreshButton.Enabled = context is not null && !_stopped;
        clearButton.Enabled = context is not null && !_stopped;
        context?.Presentation.SizeButtons(refreshButton, clearButton);
    }

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_stopped || !_loading.IsCompleted) return _loading;
        return _loading = LoadCoreAsync(cancellationToken);
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        var context = _context;
        var revision = _revision;
        if (context is null || _directory is null) { statusLabel.Text = "Mandantenauswahl benötigt den Einstellungsdialog des Hosts."; return; }
        if (_connectionChanged?.Invoke() == true)
        { statusLabel.Text = "Geänderten Zugang zuerst speichern und die Anwendung neu starten. Danach Mandanten laden."; return; }
        if (_connectionReady?.Invoke() != true)
        { statusLabel.Text = "Lexware-Zugang einrichten, speichern und die Anwendung neu starten. Danach Mandanten laden."; return; }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);
        var token = linked.Token;
        using var waiting = context.Presentation.BeginWait(this);
        refreshButton.Enabled = companyEdit.Enabled = clearButton.Enabled = false;
        statusLabel.Text = "Mandanten werden geladen …";
        try
        {
            if (context is IPluginSettingsSourceAccess access) await access.EnsureSourceAccessAsync(token);
            var companies = await _directory.GetCompaniesAsync(token);
            token.ThrowIfCancellationRequested();
            if (_stopped || revision != _revision) return;
            if (context is IPluginSettingsSourceAccess current) await current.EnsureSourceAccessAsync(token);
            if (companies.IsDefault || companies.Length > 1000 || companies.Any(company => company?.Key is null
                || company.Key.ProductLine != _productLine || string.IsNullOrWhiteSpace(company.DisplayName)
                || company.DisplayName.Length > 256 || company.DisplayName.Any(char.IsControl))
                || companies.Select(company => company.Key).Distinct().Count() != companies.Length)
                throw new InvalidOperationException("Invalid company catalog.");
            _companies = companies.OrderBy(company => company.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToImmutableArray();
            _binding = true;
            try
            {
                companyEdit.Properties.DataSource = _companies.ToArray();
                companyEdit.EditValue = _companies.Any(company => company.Key == context.Companies.SelectedCompany)
                    ? context.Companies.SelectedCompany : null;
            }
            finally { _binding = false; }
            statusLabel.Text = _companies.IsEmpty ? "Keine freigegebenen Mandanten verfügbar."
                : context.Companies.SelectedCompany is not null && companyEdit.EditValue is null
                    ? "Der gespeicherte Mandant ist nicht verfügbar. Bitte eine gültige Auswahl treffen."
                    : "Mandant auswählen. Die Auswahl wird mit den Einstellungen gespeichert.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception failure)
        {
            if (_stopped || revision != _revision) return;
            _companies = [];
            _binding = true;
            try { companyEdit.Properties.DataSource = Array.Empty<Company>(); companyEdit.EditValue = null; }
            finally { _binding = false; }
            statusLabel.Text = _describeFailure?.Invoke(failure) ?? "Mandanten konnten nicht geladen werden. Lexware-Zugang und Verbindung prüfen.";
        }
        finally
        {
            if (!_stopped && revision == _revision)
            {
                refreshButton.Enabled = clearButton.Enabled = true;
                companyEdit.Enabled = !_companies.IsEmpty;
            }
        }
    }

    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        await _loading.WaitAsync(cancellationToken);
        if (HasUnsavedChanges && _context?.Companies.SelectedCompany is { } company
            && !_companies.Any(item => item.Key == company))
            throw new InvalidOperationException("Selected company is not in the authorized catalog.");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;
        _stopped = true;
        await _stopping.CancelAsync();
        await _loading;
    }
    private async void Refresh_Click(object? sender, EventArgs e) => await LoadAsync();
    private void Clear_Click(object? sender, EventArgs e)
    {
        if (_context is null || _stopped) return;
        _context.Companies.StageSelection(null);
        companyEdit.EditValue = null;
        statusLabel.Text = "Auswahl aufgehoben. Mit „Speichern“ übernehmen.";
    }
    private void Company_Changed(object? sender, EventArgs e)
    {
        if (_binding || _stopped || _context is null) return;
        var key = companyEdit.EditValue as CompanyKey;
        if (key is not null && !_companies.Any(company => company.Key == key)) return;
        _context.Companies.StageSelection(key);
        statusLabel.Text = "Mandantenauswahl geändert. Mit „Speichern“ übernehmen.";
    }
    private void CompanyLayout_Layout(object? sender, LayoutEventArgs e)
    {
        if (_sizing || layout.ColumnStyles.Count < 3) return;
        _sizing = true;
        try
        {
            var labelWidth = companyLabel.GetPreferredSize(Size.Empty).Width + companyLabel.Margin.Horizontal;
            var available = Math.Max(1, layout.ClientSize.Width - layout.Padding.Horizontal - labelWidth);
            var width = Math.Min(available, (int)Math.Ceiling(Font.GetHeight(DeviceDpi) * 24) + LogicalToDeviceUnits(24));
            if (Math.Abs(layout.ColumnStyles[1].Width - width) >= 1) layout.ColumnStyles[1].Width = width;
        }
        finally { _sizing = false; }
    }
    private void CompanySize_Changed(object? sender, EventArgs e)
    {
        if (_sizingButtons || _context is null || _stopped) return;
        _sizingButtons = true;
        try { _context.Presentation.SizeButtons(refreshButton, clearButton); }
        finally { _sizingButtons = false; }
    }
    private void DisposePage()
    {
        if (_disposed) return;
        _disposed = true;
        _stopped = true; _stopping.Cancel(); _stopping.Dispose();
        _context = null; _directory = null; _connectionReady = null; _connectionChanged = null; _describeFailure = null;
        _companies = [];
    }
}
