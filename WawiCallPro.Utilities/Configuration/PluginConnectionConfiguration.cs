using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Uses the validated startup snapshot; changed credentials require a host restart.</summary>
public sealed class PluginConnectionConfiguration(ApplicationConfigurationService configuration, ISecretProtector secrets)
    : IPluginConnectionConfiguration
{
    public ProConnectionConfiguration? ReadPro()
    {
        var settings = configuration.Current.Options.Erp.Pro;
        if (settings is null) return null;
        using var password = secrets.Unprotect(settings.Password!, ProErpSettings.PasswordPurpose);
        using var key = secrets.Unprotect(settings.ClientKey!, ProErpSettings.ClientKeyPurpose);
        return new ProConnectionConfiguration
        {
            ApiUrl = settings.ApiUrl, InstallationId = settings.InstallationId, Username = settings.Username,
            Password = password.Reveal(), ClientKey = key.Reveal(),
            TimeoutSeconds = settings.TimeoutSeconds, TokenTimeoutSeconds = settings.TokenTimeoutSeconds
        };
    }

    public string? ReadFinOfficeInstallationId() => configuration.Current.Options.Erp.FinOfficeInstallationId;
}
