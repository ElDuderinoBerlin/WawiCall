using Microsoft.Extensions.Hosting;
using WawiCallPro.Interfaces.Hosting;

namespace WawiCallPro.Utilities.Hosting;

/// <summary>A small, safe startup report; configuration and external connectors are not implemented yet.</summary>
public sealed record HostDiagnosticSnapshot(
    string Application,
    string ProductLine,
    bool HostStarted,
    string Dispatcher,
    bool CompanySelected)
{
    public bool ConnectionConfigured => false;
}

public sealed class HostDiagnostics(ApplicationDescriptor application, ICompanyContext companies,
    IUiDispatcher dispatcher, IHostApplicationLifetime lifetime)
{
    public async Task<HostDiagnosticSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var selected = false;
        await dispatcher.InvokeAsync(() => selected = companies.Current.Company is not null, cancellationToken)
            .ConfigureAwait(false);
        return new HostDiagnosticSnapshot(application.Name, application.ProductLine,
            lifetime.ApplicationStarted.IsCancellationRequested, dispatcher.GetType().Name, selected);
    }
}
