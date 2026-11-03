using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WawiCallPro.Utilities.Hosting;

/// <summary>Owns one standard host run, including cleanup after partial startup. It does not run services itself.</summary>
public static class HostExecution
{
    /// <summary>
    /// Preserves the caller's synchronization context for presentation startup and cleanup.
    /// Body, stop and disposal failures remain observable, including when more than one stage fails.
    /// </summary>
    public static async Task RunAsync(IHost host, Func<CancellationToken, Task> body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(body);
        var failures = new List<Exception>();
        try
        {
            await host.StartAsync(cancellationToken);
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            await body(lifetime.ApplicationStopping);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try { await host.StopAsync(); }
        catch (Exception exception) { failures.Add(exception); }
        try
        {
            if (host is IAsyncDisposable asynchronousHost) await asynchronousHost.DisposeAsync();
            else host.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Host execution and cleanup failed.", failures);
    }
}
