using Quantarix.Lexware.APIService;
using Quantarix.Lexware.APIService.Models;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Plugins.Lexware.Pro;

public interface IProApiSession : IAsyncDisposable
{
    Task<ProInstallation> GetInstallationAsync(CancellationToken cancellationToken);
    Task<List<GlobalCompany>> GetCompaniesAsync(CancellationToken cancellationToken);

    /// <summary>Read-only operation with a scoped company and at most one renewal after explicit token rejection.</summary>
    Task<T> ReadCompanyAsync<T>(CompanyKey company,
        Func<ILexwareApiServiceClient, string, CancellationToken, Task<T?>> read, CancellationToken cancellationToken) where T : class;
}

/// <summary>Owns the HTTP transport and serializes token and company scopes, including asynchronous shutdown.</summary>
public sealed class ProApiSession(IProInstallationSource installations, IProApiConnectionFactory connections,
    TimeProvider clock) : IProApiSession, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1);
    private ProInstallation? _installation;
    private IProApiConnection? _connection;
    private string? _token;
    private DateTimeOffset _renewAfter;
    private bool _disposed;

    public async Task<ProInstallation> GetInstallationAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { ObjectDisposedException.ThrowIf(_disposed, this); return _installation ??= installations.Detect(); }
        finally { _gate.Release(); }
    }

    public Task<List<GlobalCompany>> GetCompaniesAsync(CancellationToken cancellationToken) =>
        ReadAsync(null, (client, token, ct) => client.GetCompaniesAsync(token, ct), true, cancellationToken);

    public Task<T> ReadCompanyAsync<T>(CompanyKey company,
        Func<ILexwareApiServiceClient, string, CancellationToken, Task<T?>> read, CancellationToken cancellationToken) where T : class
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(read);
        return ReadAsync(company, read, false, cancellationToken);
    }

    private async Task<T> ReadAsync<T>(CompanyKey? company,
        Func<ILexwareApiServiceClient, string, CancellationToken, Task<T?>> read, bool expectCompanies,
        CancellationToken cancellationToken) where T : class
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _installation ??= installations.Detect();
            if (company is not null && (company.ProductLine != LexwareProductLine.Pro || company.InstallationId != _installation.Id))
                throw new ProAdapterException(ProFailure.ForeignCompany);
            _connection ??= connections.Create(_installation);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (_token is null || clock.GetUtcNow() >= _renewAfter) await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
                _connection.Reset(expectCompanies);
                // Global company enumeration deliberately has no company scope/header.
                using var scope = company is null ? null : _connection.Client.UseCompany(company.ExternalId);
                var result = await read(_connection.Client, _token!, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (_connection.Failure == ProFailure.InvalidToken)
                {
                    _token = null;
                    if (attempt == 0) continue;
                }
                if (_connection.Failure is { } failure) throw new ProAdapterException(failure);
                return result ?? throw new ProAdapterException(ProFailure.InvalidResponse);
            }
            throw new ProAdapterException(ProFailure.AuthenticationFailed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ObjectDisposedException) { throw; }
        catch (ProAdapterException) { throw; }
        catch { throw new ProAdapterException(ProFailure.ApiUnavailable); }
        finally { _gate.Release(); }
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        _connection!.Reset();
        _token = await _connection.Client.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        // GetTokenAsync catches TaskCanceledException itself; restore caller cancellation here.
        cancellationToken.ThrowIfCancellationRequested();
        if (_connection.Failure is not null || string.IsNullOrWhiteSpace(_token))
        {
            _token = null;
            throw new ProAdapterException(_connection.Failure ?? ProFailure.AuthenticationFailed);
        }
        // Lexware may issue encrypted opaque tokens. Do not invent an expiry by decoding unverified claims.
        _renewAfter = clock.GetUtcNow().AddMinutes(5);
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            _token = null;
            _connection?.Dispose();
            _connection = null;
        }
        finally { _gate.Release(); }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
