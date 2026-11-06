using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Quantarix.Pcfk.DataAccess.Domain;
using Quantarix.Pcfk.DataAccess.Infrastructure;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Plugins.Lexware.FinOffice;

public sealed record FinOfficeInstallation(string Id, ImmutableArray<Company> Companies,
    ImmutableDictionary<string, PcfkMandantInfo> Mandants);

public interface IFinOfficeInstallationSource
{
    Task<FinOfficeInstallation> DetectAsync(CancellationToken cancellationToken = default);
}

/// <summary>Uses PCFK registry discovery and its company catalog, on a worker and with read-only connections.</summary>
public sealed class FinOfficeInstallationSource(IPcfkInstallationManager manager, FinOfficeConnectionOptions options) : IFinOfficeInstallationSource
{
    public Task<FinOfficeInstallation> DetectAsync(CancellationToken cancellationToken = default)
        => Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            PcfkInstallationInfo installation;
            try { installation = await manager.GetInstallationAsync(readOnly: true).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch { throw new FinOfficeAdapterException(FinOfficeFailure.InstallationUnavailable); }
            cancellationToken.ThrowIfCancellationRequested();
            try { return Map(installation, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (FinOfficeAdapterException) { throw; }
            catch { throw new FinOfficeAdapterException(FinOfficeFailure.InvalidCatalog); }
        }, cancellationToken);

    private FinOfficeInstallation Map(PcfkInstallationInfo installation, CancellationToken cancellationToken)
    {
        if (installation is null || !IsAbsolutePath(installation.PcfkDatenPath)
            || !IsAbsolutePath(installation.CompaniesDatabasePath) || installation.Mandanten is null
            || !File.Exists(installation.CompaniesDatabasePath))
            throw new FinOfficeAdapterException(FinOfficeFailure.InvalidCatalog);
        if (options.InstallationId is not null && string.IsNullOrWhiteSpace(options.InstallationId))
            throw new FinOfficeAdapterException(FinOfficeFailure.ConfigurationRequired);
        // Windows paths are case-insensitive. Aliases and relocations require an explicit persistent ID.
        var canonical = Path.GetFullPath(installation.CompaniesDatabasePath).ToUpperInvariant();
        var id = options.InstallationId ?? "lexware-finoffice/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var companies = ImmutableArray.CreateBuilder<Company>();
        var mandants = ImmutableDictionary.CreateBuilder<string, PcfkMandantInfo>(StringComparer.Ordinal);
        foreach (var mandant in installation.Mandanten)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (mandant?.Firma is null || mandant.Nr <= 0 || string.IsNullOrWhiteSpace(mandant.Name)
                || !IsAbsolutePath(mandant.DatabasePath) || !IsAbsolutePath(mandant.DirectoryPath)
                || !string.Equals(Path.GetDirectoryName(Path.GetFullPath(mandant.DatabasePath)),
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(mandant.DirectoryPath)), StringComparison.OrdinalIgnoreCase))
                throw new FinOfficeAdapterException(FinOfficeFailure.InvalidCatalog);
            if (!File.Exists(mandant.DatabasePath)) throw new FinOfficeAdapterException(FinOfficeFailure.CompanyUnavailable);
            var externalId = mandant.Nr.ToString(CultureInfo.InvariantCulture);
            if (!mandants.TryAdd(externalId, mandant)) throw new FinOfficeAdapterException(FinOfficeFailure.InvalidCatalog);
            companies.Add(new Company(new CompanyKey(LexwareProductLine.FinOffice, id, externalId), mandant.Name));
        }
        return new FinOfficeInstallation(id, companies.OrderBy(company => company.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(company => company.Key.ExternalId, StringComparer.Ordinal).ToImmutableArray(), mandants.ToImmutable());
    }

    private static bool IsAbsolutePath(string? path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path);
}
