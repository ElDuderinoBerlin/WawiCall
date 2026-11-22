using System.Security.Cryptography;
using System.Text;
using Quantarix.Lexware.APIService;
using Quantarix.Lexware.Core.Registry;

namespace WawiCallPro.Plugins.Lexware.Pro;

public sealed record ProRegistryPaths(string? Data, string? ApiInstall, string? ApiProgram);

public interface IProRegistry
{
    ProRegistryPaths ReadPaths();
    string? ReadApiInstallationDirectory() => ReadPaths().ApiInstall;
    string? ReadApiProgramDirectory() => ReadPaths().ApiProgram;
    string? ReadDataDirectory() => ReadPaths().Data;
}

public sealed class ProRegistry : IProRegistry
{
    public string? ReadApiInstallationDirectory() => LexwareRegistry.GetApiServiceInstallPath();
    public string? ReadApiProgramDirectory() => LexwareRegistry.GetApiServicePath();
    public string? ReadDataDirectory() => LexwareRegistry.GetDatenPath();
    public ProRegistryPaths ReadPaths() => new(LexwareRegistry.GetDatenPath(),
        LexwareRegistry.GetApiServiceInstallPath(), LexwareRegistry.GetApiServicePath());
}

public sealed record ProInstallation(string Id, Uri ApiUrl, string? PropertyStorePath);

public interface IProInstallationSource
{
    ProInstallation Detect();
}

/// <summary>Reads only local installation metadata, never client keys from Lexware or another product.</summary>
public sealed class ProInstallationSource(IProRegistry registry, ProConnectionOptions options) : IProInstallationSource
{
    public ProInstallation Detect()
    {
        try
        {
            var resolver = new LexwareApiUrlResolver(registry.ReadApiInstallationDirectory, registry.ReadApiProgramDirectory, File.ReadAllText);
            var url = new Uri(resolver.Resolve(), UriKind.Absolute);
            string? data;
            try { data = registry.ReadDataDirectory(); } catch { data = null; }
            if (!url.IsAbsoluteUri || url.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(url.UserInfo)
                || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
                throw new ProAdapterException(ProFailure.ConfigurationRequired);

            var canonical = new UriBuilder(url);
            if (url.IsLoopback) canonical.Host = Environment.MachineName.ToLowerInvariant();
            canonical.Path = canonical.Path.TrimEnd('/');
            // Endpoint identity is independent of display names and user profiles. Aliases/moved servers
            // require an explicit persistent ID so they are not silently merged with another installation.
            var id = options.InstallationId;
            if (string.IsNullOrWhiteSpace(id))
                id = "lexware-pro/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.Uri.AbsoluteUri)));
            var propertyStore = string.IsNullOrWhiteSpace(data) ? null : Path.Combine(data, "PropertyStore");
            return new ProInstallation(id, new Uri(url.AbsoluteUri.TrimEnd('/')), propertyStore);
        }
        catch (ProAdapterException) { throw; }
        catch { throw new ProAdapterException(ProFailure.InstallationUnavailable); }
    }
}
