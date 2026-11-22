using System.Text.Json.Serialization;

namespace WawiCallPro.Interfaces.Plugins;

/// <summary>Editor metadata only; saved credentials are never decrypted into controls.</summary>
public sealed class ProConnectionEditorSnapshot
{
    public bool Configured { get; init; }
    public Uri? ApiUrl { get; init; }
    public string? InstallationId { get; init; }
    public string? Username { get; init; }
    public bool PasswordStored { get; init; }
    public bool ClientKeyStored { get; init; }
    public bool DefaultClientKeyAvailable { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public int TokenTimeoutSeconds { get; init; } = 10;
    public override string ToString() => nameof(ProConnectionEditorSnapshot);
}

/// <summary>Transient, validated editor patch. Null secret values retain the saved ciphertext.</summary>
public sealed class ProConnectionEditorChange
{
    public bool Remove { get; init; }
    public Uri? ApiUrl { get; init; }
    public string? InstallationId { get; init; }
    public string? Username { get; init; }
    [JsonIgnore] public string? Password { get; init; }
    [JsonIgnore] public string? ClientKey { get; init; }
    public bool UseDefaultClientKey { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public int TokenTimeoutSeconds { get; init; } = 10;
    public override string ToString() => nameof(ProConnectionEditorChange);
}

/// <summary>A dialog-owned draft. Staging never writes files or opens a source connection.</summary>
public interface IPluginConnectionEditContext
{
    ProConnectionEditorSnapshot ReadPro();
    void StagePro(ProConnectionEditorChange change);
}

public enum PluginSettingsIssue
{
    InvalidEndpoint, InvalidInstallationId, UsernameRequired, PasswordRequired,
    ClientKeyRequired, InvalidClientKey, DefaultClientKeyUnavailable, InvalidTimeout, UnsavedChanges, ConnectionRestartRequired
}

/// <summary>Only fixed, application-defined validation text may cross the plugin/UI boundary.</summary>
public sealed class PluginSettingsValidationException(PluginSettingsIssue issue) : Exception(issue switch
{
    PluginSettingsIssue.ConnectionRestartRequired => "Geänderte Lexware-Zugangsdaten zuerst speichern und neu starten. Danach den Mandanten auswählen.",
    PluginSettingsIssue.InvalidEndpoint => "Die API-Adresse muss eine absolute HTTP-/HTTPS-Adresse ohne Zugangsdaten, Query oder Fragment sein.",
    PluginSettingsIssue.InvalidInstallationId => "Die optionale Installationskennung ist ungültig.",
    PluginSettingsIssue.UsernameRequired => "Einen gültigen API-Benutzernamen eingeben.",
    PluginSettingsIssue.PasswordRequired => "Für den neuen Zugang ist ein Kennwort erforderlich.",
    PluginSettingsIssue.ClientKeyRequired => "Einen Client-Key eingeben oder die verfügbare Buildvorgabe auswählen.",
    PluginSettingsIssue.InvalidClientKey => "Der Client-Key muss eine gültige GUID mit geschweiften Klammern sein.",
    PluginSettingsIssue.DefaultClientKeyUnavailable => "Für diesen Build ist kein vorgegebener Client-Key verfügbar.",
    PluginSettingsIssue.InvalidTimeout => "Die Zeitlimits müssen zwischen 1 und 300 Sekunden liegen.",
    _ => "Zuerst die ungespeicherten Plugin-Zugangseinstellungen speichern oder den Einstellungsdialog mit Abbrechen verlassen."
})
{
    public PluginSettingsIssue Issue { get; } = issue;
}
