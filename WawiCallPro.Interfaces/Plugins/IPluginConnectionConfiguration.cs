using System.Text.Json.Serialization;

namespace WawiCallPro.Interfaces.Plugins;

/// <summary>Startup configuration supplied by the host. Reading never contacts an ERP system.</summary>
public interface IPluginConnectionConfiguration
{
    ProConnectionConfiguration? ReadPro();
    string? ReadFinOfficeInstallationId();
}

/// <summary>Transient API inputs; plaintext is only supplied at the plugin composition boundary.</summary>
public sealed class ProConnectionConfiguration
{
    public Uri? ApiUrl { get; init; }
    public string? InstallationId { get; init; }
    [JsonIgnore] public string? Username { get; init; }
    [JsonIgnore] public string? Password { get; init; }
    [JsonIgnore] public string? ClientKey { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public int TokenTimeoutSeconds { get; init; } = 10;
    public override string ToString() => nameof(ProConnectionConfiguration);
}
