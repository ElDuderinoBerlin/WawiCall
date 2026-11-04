using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Reads the startup snapshot using complete source identity, never PreferredCompany or active UI state.</summary>
public sealed class CompanyPhoneNumberContextProvider(ApplicationConfigurationService configuration) : ICompanyPhoneNumberContextProvider
{
    public CompanyPhoneNumberContext? ForCompany(CompanyKey company)
    {
        ArgumentNullException.ThrowIfNull(company);
        var settings = configuration.Current.Options.FindCompanySettings(company);
        return settings is null ? null : new CompanyPhoneNumberContext(settings.Company, settings.DefaultPhoneRegion, settings.LocalAreaCode);
    }
}
