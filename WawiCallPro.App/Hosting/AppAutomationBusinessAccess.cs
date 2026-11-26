using WawiCallPro.Automation;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.App.Hosting;

/// <summary>Revalidates current license, installation and company membership; cached catalogs never grant source access.</summary>
internal sealed class AppAutomationBusinessAccess(AppAutomationState state, IProductLicenseService licenses,
    LicenseProductIdentity product, ILexwareInstallation installation, IProductPluginRuntime runtime,
    RuntimeFailureState failures) : IAutomationBusinessAccess
{
    public async Task RequireAsync(CompanyKey company, CancellationToken cancellationToken)
    {
        state.RequireAccess(AutomationAccess.Business, company);
        try
        {
            await CheckLicenseAsync(cancellationToken);
            CheckSource();
            await runtime.UseAsync(async (plugin, token) =>
            {
                if (plugin.ProductLine != company.ProductLine || company.ProductLine != state.GetStatus().ProductLine)
                    throw new AutomationFault(AutomationFailure.AccessDenied);
                var companies = await plugin.Directory.GetCompaniesAsync(token);
                if (companies.IsDefault || companies.Length > 1000 || companies.Any(item => item?.Key is null)
                    || companies.Select(item => item.Key).Distinct().Count() != companies.Length
                    || !companies.Any(item => item.Key == company)) throw new AutomationFault(AutomationFailure.AccessDenied);
                await CheckLicenseAsync(token);
                CheckSource(); state.RequireAccess(AutomationAccess.Business, company);
                return true;
            }, cancellationToken);
        }
        catch (Exception error) when (error is LicenseAccessException or LexwareUnavailableException or UnauthorizedAccessException)
        { throw new AutomationFault(AutomationFailure.AccessDenied); }
    }
    private async Task CheckLicenseAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (failures.Failure is not null) throw new AutomationFault(AutomationFailure.AccessDenied);
        if (product.Required && (await licenses.ValidateAsync(token)).Status != LicenseStatus.Valid)
            throw new AutomationFault(AutomationFailure.AccessDenied);
    }
    private void CheckSource()
    {
        var installed = installation.Check();
        var testSource = (runtime as IProductPluginSelection)?.ActivePluginId == ProductPluginIds.JtlWawi;
        if (!testSource && !installed.IsAvailable || !runtime.IsLoaded
            || runtime is IProductPluginAvailability availability && !availability.CanUseActivePlugin)
            throw new AutomationFault(AutomationFailure.AccessDenied);
    }
}
