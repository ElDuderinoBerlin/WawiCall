using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Reads the validated committed ERP snapshot. Personal passwords stay DPAPI protected.</summary>
public sealed class PluginConnectionConfiguration(ApplicationConfigurationService configuration, ISecretProtector secrets)
    : IPluginConnectionConfiguration
{
    public long Revision => configuration.ErpRevision;
    public ProConnectionConfiguration? ReadPro()
    {
        var settings = configuration.Current.Options.Erp.Pro;
        if (settings is null) return null;
        using var password = secrets.Unprotect(settings.Password!, ProErpSettings.PasswordPurpose);
        using var key = settings.ClientKey is null ? null : secrets.Unprotect(settings.ClientKey, ProErpSettings.ClientKeyPurpose);
        var technical = configuration.Current.ProductFile is { } product ? new ProductAppSettings(product).Read()["LexwareApi"] : null;
        return new ProConnectionConfiguration
        {
            ApiUrl = settings.ApiUrl, InstallationId = configuration.Current.ProductFile is not null ? technical?["InstallationId"]?.GetValue<string>() : settings.InstallationId, Username = settings.Username,
            Password = password.Reveal(), ClientKey = key?.Reveal() ?? (configuration.Current.ProductFile is { } file ? new ProductAppSettings(file).ReadClientKey() : LexwareClientDefaults.GetProClientKey()),
            TimeoutSeconds = configuration.Current.ProductFile is not null ? technical?["TimeoutSeconds"]?.GetValue<int>() ?? 30 : settings.TimeoutSeconds,
            TokenTimeoutSeconds = configuration.Current.ProductFile is not null ? technical?["TokenTimeoutSeconds"]?.GetValue<int>() ?? 10 : settings.TokenTimeoutSeconds
        };
    }

    public string? ReadFinOfficeInstallationId() => configuration.Current.Options.Erp.FinOfficeInstallationId;
}
