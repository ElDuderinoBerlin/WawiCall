using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Companies;

namespace WawiCallPro.Utilities.Hosting;

/// <summary>Registered before workers so selection cancellation is cleaned up after worker StopAsync calls.</summary>
internal sealed class CompanyContextLifetime(CompanyContext companies) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken) =>
        await companies.DisposeAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
}
