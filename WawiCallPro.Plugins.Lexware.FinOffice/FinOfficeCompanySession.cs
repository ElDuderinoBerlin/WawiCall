using System.Data;
using Quantarix.Pcfk.DataAccess.Infrastructure;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Plugins.Lexware.FinOffice;

public interface IFinOfficeCompanySession
{
    Task<T> ReadAsync<T>(CompanyKey company, Func<IDbConnection, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

/// <summary>Each operation owns a fresh read-only connection for the explicitly selected company.</summary>
public sealed class FinOfficeCompanySession(IFinOfficeInstallationSource source, IPcfkInstallationManager manager) : IFinOfficeCompanySession
{
    public async Task<T> ReadAsync<T>(CompanyKey company, Func<IDbConnection, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        if (company.ProductLine != LexwareProductLine.FinOffice) throw new FinOfficeAdapterException(FinOfficeFailure.ForeignCompany);
        var installation = await source.DetectAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(installation.Id, company.InstallationId, StringComparison.Ordinal))
            throw new FinOfficeAdapterException(FinOfficeFailure.ForeignCompany);
        if (!installation.Mandants.TryGetValue(company.ExternalId, out var mandant))
            throw new FinOfficeAdapterException(FinOfficeFailure.CompanyUnavailable);
        return await Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var connection = await manager.CreateMandantConnectionAsync(mandant, readOnly: true).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var result = await operation(connection, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (FinOfficeAdapterException) { throw; }
            catch { throw new FinOfficeAdapterException(FinOfficeFailure.ReadFailed); }
        }, cancellationToken).ConfigureAwait(false);
    }
}
