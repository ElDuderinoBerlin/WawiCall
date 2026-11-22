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
    internal ApplicationOptions ApplyTo(ApplicationOptions options) => connection.ApplyTo(options) with
    { User = options.User with { PreferredCompany = SelectedCompany } };
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
