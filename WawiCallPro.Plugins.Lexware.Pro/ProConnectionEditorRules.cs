using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugins.Lexware.Pro;

/// <summary>Transient input, deliberately excluded from diagnostic representations.</summary>
internal sealed class ProConnectionEditorInput
{
    internal bool Enabled;
    internal string Endpoint = "", InstallationId = "", Username = "", Password = "", ClientKey = "";
    internal bool UseDefaultClientKey;
    internal int TimeoutSeconds = 30, TokenTimeoutSeconds = 10;
    public override string ToString() => nameof(ProConnectionEditorInput);
}

internal static class ProConnectionEditorRules
{
    internal static ProConnectionEditorChange Read(ProConnectionEditorInput input, ProConnectionEditorSnapshot saved)
    {
        if (!input.Enabled) return new() { Remove = true };
        Uri? endpoint = null;
        if (!string.IsNullOrWhiteSpace(input.Endpoint)
            && (!Uri.TryCreate(input.Endpoint.Trim(), UriKind.Absolute, out endpoint) || endpoint.Scheme is not ("http" or "https")
                || endpoint.UserInfo.Length > 0 || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0))
            throw new PluginSettingsValidationException(PluginSettingsIssue.InvalidEndpoint);
        var installation = input.InstallationId.Trim();
        if (installation.Length > 200 || installation.Any(char.IsControl))
            throw new PluginSettingsValidationException(PluginSettingsIssue.InvalidInstallationId);
        var username = input.Username.Trim();
        if (username.Length is 0 or > 256 || username.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
            throw new PluginSettingsValidationException(PluginSettingsIssue.UsernameRequired);
        if (input.Password.Length == 0 && !saved.PasswordStored)
            throw new PluginSettingsValidationException(PluginSettingsIssue.PasswordRequired);
        if (input.UseDefaultClientKey && !saved.DefaultClientKeyAvailable)
            throw new PluginSettingsValidationException(PluginSettingsIssue.DefaultClientKeyUnavailable);
        var keyText = input.ClientKey.Trim();
        if (!input.UseDefaultClientKey && keyText.Length == 0 && !saved.ClientKeyStored)
            throw new PluginSettingsValidationException(PluginSettingsIssue.ClientKeyRequired);
        if (!input.UseDefaultClientKey && keyText.Length > 0
            && (!Guid.TryParseExact(keyText, "B", out var key) || key == Guid.Empty))
            throw new PluginSettingsValidationException(PluginSettingsIssue.InvalidClientKey);
        if (input.TimeoutSeconds is < 1 or > 300 || input.TokenTimeoutSeconds is < 1 or > 300)
            throw new PluginSettingsValidationException(PluginSettingsIssue.InvalidTimeout);
        return new()
        {
            ApiUrl = endpoint, InstallationId = installation.Length == 0 ? null : installation, Username = username,
            Password = input.Password.Length == 0 ? null : input.Password,
            ClientKey = input.UseDefaultClientKey || keyText.Length == 0 ? null : keyText,
            UseDefaultClientKey = input.UseDefaultClientKey,
            TimeoutSeconds = input.TimeoutSeconds, TokenTimeoutSeconds = input.TokenTimeoutSeconds
        };
    }
}
