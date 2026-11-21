using System.Collections.Immutable;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Core.Companies;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.App.Views;

/// <summary>Displays the active plugin's company catalog; source discovery and authorization remain in that plugin.</summary>
public partial class CompanySelector : XtraUserControl
{
    private IBusinessDirectory? _directory;
    private CompanyContext? _context;
    private IProductPluginRuntime? _plugins;
    private LexwareProductLine _productLine;
    private ImmutableArray<Company> _companies = [];
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<Task> _pending = [];
    private CancellationTokenSource? _request;
    private bool _binding, _stopped, _disposed;
    private string? _catalogPlugin;
    internal event EventHandler? LoadFailed;

    public CompanySelector()
    {
        InitializeComponent();
        refreshButton.ImageOptions.SvgImage = RibbonIconHelper.Load("Refresh");
        UiButtonSizing.Attach(this);
    }

    internal void Bind(IBusinessDirectory directory, CompanyContext context, IProductPluginRuntime plugins, LexwareProductLine productLine)
    {
        _directory = directory; _context = context; _plugins = plugins; _productLine = productLine;
    }

    private string? PluginId => (_plugins as IProductPluginSelection)?.ActivePluginId;
    private bool Available => _plugins?.IsLoaded == true
        && (_plugins is not IProductPluginAvailability availability || availability.CanUseActivePlugin);

    internal int GetMinimumContentWidth() => Math.Max(companyLabel.PreferredSize.Width + companyLabel.Margin.Horizontal,
        refreshButton.GetPreferredSize(Size.Empty).Width + refreshButton.Margin.Horizontal) + layout.Padding.Horizontal;

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (layout is null) return base.GetPreferredSize(proposedSize);
        var width = proposedSize.Width > 0 ? proposedSize.Width : Math.Max(1, Width);
        width = Math.Max(width, GetMinimumContentWidth());
        var contentWidth = Math.Max(1, width - layout.Padding.Horizontal);
        var height = layout.Padding.Vertical;
        foreach (Control control in new Control[] { companyLabel, companyEdit, refreshButton, statusLabel })
            height += control.GetPreferredSize(new Size(Math.Max(1, contentWidth - control.Margin.Horizontal), 0)).Height + control.Margin.Vertical;
        return new Size(width, height);
    }

    internal Task ReloadAsync()
    {
        if (_stopped || _directory is null) return Task.CompletedTask;
        _request?.Cancel();
        var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _request = request;
        var task = LoadAsync(request, PluginId);
        Track(task);
        return task;
    }

    private async Task LoadAsync(CancellationTokenSource request, string? plugin)
    {
        // Register the task before a synchronous source can finish or a UI callback can start another request.
        await Task.Yield();
        var token = request.Token;
        using var waiting = new UiWaitCursor(this);
        try
        {
            token.ThrowIfCancellationRequested();
            SetCatalog([]);
            companyEdit.Enabled = false;
            refreshButton.Enabled = false;
            statusLabel.Text = "Mandanten werden geladen …";
            if (!Available)
            {
                await _context!.SelectAsync(null);
                statusLabel.Text = "Warenwirtschaft nicht verfügbar. Siehe Log-Fenster.";
                return;
            }
            var companies = await _directory!.GetCompaniesAsync(token);
            token.ThrowIfCancellationRequested();
            if (_stopped || request != _request) return;
            if (!Available || plugin != PluginId)
            {
                await _context!.SelectAsync(null);
                statusLabel.Text = "Plugin geändert. Mandanten bitte neu laden.";
                return;
            }
            if (companies.IsDefault || companies.Length > 1000
                || companies.Any(company => company?.Key is null || company.Key.ProductLine != _productLine
                    || string.IsNullOrWhiteSpace(company.DisplayName) || company.DisplayName.Length > 256
                    || company.DisplayName.Any(char.IsControl))
                || companies.Select(company => company.Key).Distinct().Count() != companies.Length)
                throw new InvalidOperationException("Invalid company catalog.");
            _catalogPlugin = plugin;
            var selected = _context!.Current.Company?.Key;
            SetCatalog(companies.OrderBy(company => company.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToImmutableArray());
            var current = companies.FirstOrDefault(company => company.Key == selected);
            _binding = true;
            try { companyEdit.EditValue = current?.Key; }
            finally { _binding = false; }
            if (_context.Current.Company != current) await _context.SelectAsync(current);
            statusLabel.Text = companies.IsEmpty ? "Keine freigegebenen Mandanten verfügbar."
                : current is null ? "Mandant auswählen." : "Mandant ausgewählt.";
            companyEdit.Enabled = !companies.IsEmpty;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            if (_stopped || request != _request) return;
            SetCatalog([]);
            await _context!.SelectAsync(null);
            statusLabel.Text = "Mandanten konnten nicht geladen werden. Wawi-Zugang und Plugin prüfen.";
            LoadFailed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            if (!_stopped && request == _request) refreshButton.Enabled = true;
            if (request == _request) _request = null;
            request.Dispose();
        }
    }

    private void SetCatalog(ImmutableArray<Company> companies)
    {
        _binding = true;
        try
        {
            _companies = companies;
            companyEdit.EditValue = null;
            companyEdit.Properties.DataSource = companies.ToArray();
        }
        finally { _binding = false; }
    }

    private void Refresh_Click(object? sender, EventArgs e) => _ = ReloadAsync();
    private void Company_Changed(object? sender, EventArgs e)
    {
        if (_binding || _stopped || _context is null) return;
        Track(SelectAsync());
    }

    private async Task SelectAsync()
    {
        try
        {
            var key = companyEdit.EditValue as CompanyKey;
            var company = Available && PluginId == _catalogPlugin ? _companies.FirstOrDefault(company => company.Key == key) : null;
            if (company is null)
            {
                _binding = true;
                try { companyEdit.EditValue = null; }
                finally { _binding = false; }
            }
            await _context!.SelectAsync(company);
            if (!_stopped) statusLabel.Text = company is null ? "Mandant auswählen." : "Mandant ausgewählt.";
        }
        catch (Exception)
        {
            if (!_stopped) statusLabel.Text = "Mandantenauswahl fehlgeschlagen. Bitte erneut laden.";
        }
    }

    private void Track(Task task)
    {
        _pending.RemoveAll(item => item.IsCompleted);
        _pending.Add(task);
    }

    internal async Task ResetAsync()
    {
        _request?.Cancel();
        await Task.WhenAll(_pending.ToArray());
        if (_stopped) return;
        SetCatalog([]); _catalogPlugin = null; companyEdit.Enabled = false;
        statusLabel.Text = "Mandanten noch nicht geladen.";
    }

    internal async Task StopAsync()
    {
        _stopped = true;
        if (!_disposed) await _lifetime.CancelAsync();
        await Task.WhenAll(_pending.ToArray());
    }

    private void DisposePage()
    {
        if (_disposed) return;
        _disposed = true;
        _stopped = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
