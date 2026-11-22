using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugin.Windows;

/// <summary>Plugin-exported complete company settings page for products without an additional connection editor.</summary>
public sealed class PluginCompanySettingsView : IPluginSettingsView
{
    private readonly PluginCompanySettingsPage _page = new();
    private readonly IBusinessDirectory _directory;
    private readonly LexwareProductLine _line;
    public PluginCompanySettingsView(IBusinessDirectory directory, LexwareProductLine line, IPluginConnectionEditContext context)
    {
        _directory = directory; _line = line;
        Reload(context);
    }
    public Control View => _page;
    public bool SupportsCompanyValidation => true;
    public async Task<Company?> ResolveCompanyAsync(CompanyKey company, CancellationToken token = default)
        => (await _directory.GetCompaniesAsync(token)).SingleOrDefault(item => item.Key == company);
    public bool HasUnsavedChanges => _page.HasUnsavedChanges;
    public void Reload(IPluginConnectionEditContext context) => _page.Bind(context as IPluginSettingsPageContext, _directory, _line,
        () => _directory is not IBusinessDirectoryConnectionState connection || connection.IsConnectionConfigured);
    public void ApplyChanges(IPluginConnectionEditContext context) { }
    public Task LoadAsync(CancellationToken cancellationToken = default) => _page.LoadAsync(cancellationToken);
    public Task ValidateAsync(CancellationToken cancellationToken = default) => _page.ValidateAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken = default) => _page.StopAsync(cancellationToken);
    public void Dispose() => _page.Dispose();
}
