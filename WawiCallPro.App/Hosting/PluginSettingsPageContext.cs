using System.Collections.Immutable;
using DevExpress.XtraEditors;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Hosting;

/// <summary>Dialog-owned connection/selection drafts and narrow UI helpers. Publication is the host's explicit save step.</summary>
internal sealed class PluginSettingsPageContext(PluginConnectionEditContext connection, AppWorkspace workspace, CompanyKey? selected)
    : IPluginConnectionEditContext, IPluginConnectionPreview, IPluginSettingsPageContext, IPluginCompanySelectionDraft, IPluginSettingsPresentation, IPluginSettingsSourceAccess
{
    private readonly string? _pluginId = (workspace.Plugins as IProductPluginSelection)?.ActivePluginId;
    private readonly CompanyKey? _initialSelection = selected;
    private WawiCallPro.Interfaces.Companies.Company[] _scope = [];
    private HashSet<CompanyKey> _initialScope = [];
    public IReadOnlyList<Company> SelectedCompanies => _scope;
    internal bool HasScopeChanges => !_initialScope.SetEquals(_scope.Select(company => company.Key));
    internal void InitializeScope(ApplicationOptions options)
    {
        _scope = options.Companies.Where(item => item.IncludeInPhoneCache && item.Company.ProductLine.ToString() == workspace.Application.ProductLine)
            .Select(item => new Company(item.Company, item.DisplayName ?? "Gespeicherter Mandant")).ToArray();
        _initialScope = _scope.Select(item => item.Key).ToHashSet();
    }
    public void StageSelections(IReadOnlyList<Company> companies)
    {
        if (companies.Count > 1000 || companies.Any(item => item?.Key is null || item.Key.ProductLine.ToString() != workspace.Application.ProductLine)
            || companies.Select(item => item.Key).Distinct().Count() != companies.Count) throw new InvalidOperationException("Invalid company scope.");
        _scope = companies.ToArray();
    }
    internal AppUiState? LayoutState { get; set; }
    public void RestoreCompanyLayout(DevExpress.XtraGrid.Views.Grid.GridView view) => LayoutState?.Restore(view, "PluginCompanies-v2");
    public void SaveCompanyLayout(DevExpress.XtraGrid.Views.Grid.GridView view) => LayoutState?.Save(view, "PluginCompanies-v2");
    public void Report(PluginSettingsNotice notice) => workspace.ReportSettings(notice switch
    {
        PluginSettingsNotice.Loading => WawiCallPro.Utilities.Operations.OperationEvent.SettingsCompaniesLoading,
        PluginSettingsNotice.Loaded => WawiCallPro.Utilities.Operations.OperationEvent.SettingsCompaniesLoaded,
        PluginSettingsNotice.Empty => WawiCallPro.Utilities.Operations.OperationEvent.SettingsCompaniesEmpty,
        PluginSettingsNotice.Unavailable => WawiCallPro.Utilities.Operations.OperationEvent.SettingsCompaniesUnavailable,
        PluginSettingsNotice.Failed => WawiCallPro.Utilities.Operations.OperationEvent.SettingsCompaniesFailed,
        _ => WawiCallPro.Utilities.Operations.OperationEvent.SettingsSelectionChanged
    });
    public IPluginConnectionEditContext Connection => connection;
    public IPluginCompanySelectionDraft Companies => this;
    public IPluginSettingsPresentation Presentation => this;
    public CompanyKey? SelectedCompany { get; private set; } = selected;
    internal bool HasSelectionChanges => SelectedCompany != _initialSelection;
    public ProConnectionEditorSnapshot ReadPro() => connection.ReadPro();
    public void StagePro(ProConnectionEditorChange change) => connection.StagePro(change);
    public ProConnectionConfiguration? ReadProPreview(ProConnectionEditorChange change) => connection.ReadProPreview(change);
    internal IPluginSettingsHandle? CompanyValidation { get; set; }
    public void StageSelection(CompanyKey? company)
    {
        if (company is not null && company.ProductLine.ToString() != workspace.Application.ProductLine)
            throw new InvalidOperationException("Foreign company selection.");
        SelectedCompany = company;
    }
    public IDisposable BeginWait(Control owner) => new UiWaitCursor(owner);
    public void SizeButtons(params SimpleButton[] buttons) => UiButtonSizing.Apply(buttons);
    public async Task EnsureSourceAccessAsync(CancellationToken cancellationToken = default)
    {
        if (!workspace.CanUseBusinessDirectory || _pluginId != (workspace.Plugins as IProductPluginSelection)?.ActivePluginId)
            throw new InvalidOperationException("Plugin session changed.");
        await workspace.Plugins.UseAsync((_, token) => { token.ThrowIfCancellationRequested(); return Task.FromResult(true); }, cancellationToken);
        if (_pluginId != (workspace.Plugins as IProductPluginSelection)?.ActivePluginId)
            throw new InvalidOperationException("Plugin session changed.");
    }
    internal ApplicationOptions ApplyTo(ApplicationOptions options)
    {
        var next = connection.ApplyTo(options) with { User = options.User with { PreferredCompany = SelectedCompany } };
        if (!HasScopeChanges) return next;
        var settings = next.Companies.ToDictionary(item => item.Company);
        foreach (var item in settings.Values.ToArray())
            if (item.Company.ProductLine.ToString() == workspace.Application.ProductLine)
                settings[item.Company] = item with { IncludeInPhoneCache = _scope.Any(selected => selected.Key == item.Company) };
        foreach (var company in _scope)
            settings[company.Key] = settings.TryGetValue(company.Key, out var existing)
                ? existing with { IncludeInPhoneCache = true, DisplayName = company.DisplayName }
                : new CompanySettings { Company = company.Key, IncludeInPhoneCache = true, DisplayName = company.DisplayName };
        return next with { Companies = settings.Values.ToImmutableArray() };
    }
    internal async Task<Company?> ValidateSelectionAsync(CancellationToken token)
    {
        if (!HasSelectionChanges || SelectedCompany is null) return null;
        await EnsureSourceAccessAsync(token);
        if (CompanyValidation?.SupportsCompanyValidation == true)
        {
            var resolved = await CompanyValidation.ResolveCompanyAsync(SelectedCompany, token);
            await EnsureSourceAccessAsync(token);
            return resolved ?? throw new InvalidOperationException("Selected company is unavailable.");
        }
        var companies = await workspace.Directory.GetCompaniesAsync(token);
        await EnsureSourceAccessAsync(token);
        return companies.SingleOrDefault(company => company.Key == SelectedCompany)
            ?? throw new InvalidOperationException("Selected company is unavailable.");
    }
}
