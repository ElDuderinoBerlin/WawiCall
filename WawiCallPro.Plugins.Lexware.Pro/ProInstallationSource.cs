using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Quantarix.Lexware.Core.Registry;

namespace WawiCallPro.Plugins.Lexware.Pro;

public sealed record ProRegistryPaths(string? Data, string? ApiInstall, string? ApiProgram);

public interface IProRegistry
{
    ProRegistryPaths ReadPaths();
}

public sealed class ProRegistry : IProRegistry
{
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
            var paths = registry.ReadPaths();
            var url = options.ApiUrl ?? ReadUrl(paths.ApiInstall) ?? ReadUrl(paths.ApiProgram)
                ?? throw new ProAdapterException(ProFailure.InstallationUnavailable);
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
            var propertyStore = string.IsNullOrWhiteSpace(paths.Data) ? null : Path.Combine(paths.Data, "PropertyStore");
            return new ProInstallation(id, new Uri(url.AbsoluteUri.TrimEnd('/')), propertyStore);
        }
        catch (ProAdapterException) { throw; }
        catch { throw new ProAdapterException(ProFailure.InstallationUnavailable); }
    }

    private static Uri? ReadUrl(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return null;
        var path = Path.Combine(directory, "appsettings.json");
        if (!File.Exists(path)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        if (!document.RootElement.TryGetProperty("Urls", out var value)) return null;
        var first = value.GetString()?.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        first = first?.Replace("://+:", "://localhost:", StringComparison.Ordinal)
            .Replace("://*:", "://localhost:", StringComparison.Ordinal);
        if (!Uri.TryCreate(first, UriKind.Absolute, out var url))
            throw new ProAdapterException(ProFailure.InstallationUnavailable);
        if (url.Host is "0.0.0.0" or "[::]") url = new UriBuilder(url) { Host = "localhost" }.Uri;
        return url;
    }
}
