using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Maps a plugin patch into the existing encrypted settings draft for one atomic host save.</summary>
public sealed class PluginConnectionEditContext
    : IPluginConnectionEditContext, IPluginConnectionPreview
{
    private readonly ConfigurationSnapshot _snapshot;
    private readonly ISecretProtector _secrets;
    private ProErpSettings? _pro;
    private bool _staged;
    private string? _productKey;
    private readonly string? _productHash;

    internal PluginConnectionEditContext(ConfigurationSnapshot snapshot, ISecretProtector secrets)
    { _snapshot = snapshot; _secrets = secrets; _productHash = snapshot.ProductFile is { } path ? new ProductAppSettings(path).Fingerprint() : null; }

    public ProConnectionEditorSnapshot ReadPro()
    {
        var pro = _snapshot.Options.Erp.Pro;
        return new()
        {
            Configured = pro is not null, ApiUrl = pro?.ApiUrl, InstallationId = _snapshot.ProductFile is not null ? ReadTechnicalString("InstallationId") : pro?.InstallationId,
            Username = pro?.Username ?? ReadDebugUsername(), PasswordStored = pro?.Password is not null, ClientKeyStored = pro?.ClientKey is not null,
            DefaultClientKeyAvailable = ReadDefaultKey() is not null,
            InitialPassword = ReadDebugPassword(pro),
            TimeoutSeconds = _snapshot.ProductFile is not null ? ReadTechnicalInt("TimeoutSeconds", 30) : pro?.TimeoutSeconds ?? 30,
            TokenTimeoutSeconds = _snapshot.ProductFile is not null ? ReadTechnicalInt("TokenTimeoutSeconds", 10) : pro?.TokenTimeoutSeconds ?? 10
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
                ? _secrets.Protect(ReadDefaultKey()
                    ?? throw new PluginSettingsValidationException(PluginSettingsIssue.DefaultClientKeyUnavailable), ProErpSettings.ClientKeyPurpose)
                : change.ClientKey is null ? old?.ClientKey : _secrets.Protect(change.ClientKey, ProErpSettings.ClientKeyPurpose),
            TimeoutSeconds = change.TimeoutSeconds, TokenTimeoutSeconds = change.TokenTimeoutSeconds
        };
        if (_snapshot.ProductFile is not null && !change.Remove && change.ClientKey is not null)
        { _productKey = change.ClientKey; _pro = _pro! with { ClientKey = null }; }
        else if (_snapshot.ProductFile is not null && change.UseDefaultClientKey && _pro is not null) _pro = _pro with { ClientKey = null };
        else if (_snapshot.ProductFile is not null && _pro?.ClientKey is { } legacyKey)
        {
            using var original = _secrets.Unprotect(legacyKey, ProErpSettings.ClientKeyPurpose);
            if (ReadDefaultKey() != original.Reveal()) _productKey = original.Reveal();
            _pro = _pro with { ClientKey = null };
        }
        _staged = true;
    }

    private string? ReadTechnicalString(string field) => _snapshot.ProductFile is { } file ? new ProductAppSettings(file).Read()["LexwareApi"]?[field]?.GetValue<string>() : null;
    private int ReadTechnicalInt(string field, int fallback) => _snapshot.ProductFile is { } file ? new ProductAppSettings(file).Read()["LexwareApi"]?[field]?.GetValue<int>() ?? fallback : fallback;
    private string? ReadDefaultKey() => _snapshot.ProductFile is { } file
        ? new ProductAppSettings(file).ReadClientKey() : LexwareClientDefaults.GetProClientKey();
    private string? ReadDebugUsername()
    {
#if DEBUG
        if (_snapshot.ProductFile is { } file) return new ProductAppSettings(file).Read()["DebugLexware"]?["Username"]?.GetValue<string>();
#endif
        return null;
    }
    private string? ReadDebugPassword(ProErpSettings? pro)
    {
#if DEBUG
        if (pro is null && _snapshot.ProductFile is { } file)
        {
            var envelope = new ProductAppSettings(file).Read()["DebugLexware"]?["Password"]?.GetValue<string>();
            if (envelope is not null)
            {
                using var password = _secrets.Unprotect(new ProtectedSecret(envelope), ProErpSettings.PasswordPurpose);
                return password.Reveal();
            }
        }
#endif
        return null;
    }
    public ProConnectionConfiguration? ReadProPreview(ProConnectionEditorChange change)
    {
        var draft = new PluginConnectionEditContext(_snapshot, _secrets);
        draft.StagePro(change);
        var pro = draft._pro;
        if (pro is null) return null;
        using var password = _secrets.Unprotect(pro.Password!, ProErpSettings.PasswordPurpose);
        using var key = pro.ClientKey is null ? null : _secrets.Unprotect(pro.ClientKey, ProErpSettings.ClientKeyPurpose);
        return new()
        {
            ApiUrl = pro.ApiUrl, InstallationId = pro.InstallationId, Username = pro.Username, Password = password.Reveal(),
            ClientKey = draft._productKey ?? key?.Reveal() ?? ReadDefaultKey(), TimeoutSeconds = pro.TimeoutSeconds, TokenTimeoutSeconds = pro.TokenTimeoutSeconds
        };
    }

    public ApplicationOptions ApplyTo(ApplicationOptions draft) =>
        !_staged ? draft : draft with { Erp = draft.Erp with { Pro = _pro },
            ProductClientKeyEdit = _productKey is not null && _snapshot.ProductFile is { } path ? new(path, _productKey, _productHash) : null };
}
