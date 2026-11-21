using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Maps a plugin patch into the existing encrypted settings draft for one atomic host save.</summary>
public sealed class PluginConnectionEditContext
    : IPluginConnectionEditContext
{
    private readonly ConfigurationSnapshot _snapshot;
    private readonly ISecretProtector _secrets;
    private ProErpSettings? _pro;
    private bool _staged;

    internal PluginConnectionEditContext(ConfigurationSnapshot snapshot, ISecretProtector secrets)
    { _snapshot = snapshot; _secrets = secrets; }

    public ProConnectionEditorSnapshot ReadPro()
    {
        var pro = _snapshot.Options.Erp.Pro;
        return new()
        {
            Configured = pro is not null, ApiUrl = pro?.ApiUrl, InstallationId = pro?.InstallationId,
            Username = pro?.Username, PasswordStored = pro?.Password is not null, ClientKeyStored = pro?.ClientKey is not null,
            DefaultClientKeyAvailable = LexwareClientDefaults.GetProClientKey() is not null,
            TimeoutSeconds = pro?.TimeoutSeconds ?? 30, TokenTimeoutSeconds = pro?.TokenTimeoutSeconds ?? 10
        };
    }

    public void StagePro(ProConnectionEditorChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var old = _snapshot.Options.Erp.Pro;
        _pro = change.Remove ? null : new ProErpSettings
        {
            ApiUrl = change.ApiUrl, InstallationId = change.InstallationId, Username = change.Username,
            Password = change.Password is null ? old?.Password : _secrets.Protect(change.Password, ProErpSettings.PasswordPurpose),
            ClientKey = change.UseDefaultClientKey
                ? _secrets.Protect(LexwareClientDefaults.GetProClientKey()
                    ?? throw new PluginSettingsValidationException(PluginSettingsIssue.DefaultClientKeyUnavailable), ProErpSettings.ClientKeyPurpose)
                : change.ClientKey is null ? old?.ClientKey : _secrets.Protect(change.ClientKey, ProErpSettings.ClientKeyPurpose),
            TimeoutSeconds = change.TimeoutSeconds, TokenTimeoutSeconds = change.TokenTimeoutSeconds
        };
        _staged = true;
    }

    public ApplicationOptions ApplyTo(ApplicationOptions draft) =>
        !_staged ? draft : draft with { Erp = draft.Erp with { Pro = _pro } };
}
