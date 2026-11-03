using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WawiCallPro.Core.Companies;
using WawiCallPro.Utilities.Operations;

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
        var runtimeFailures = host.Services.GetRequiredService<RuntimeFailureState>();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WawiCallPro.Host");
        var companies = host.Services.GetRequiredService<CompanyContext>();
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var shutdownTimeout = host.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout;
        IHostedService[] services = [];
        var started = false;
        try
        {
            OperationEvents.Write(logger, OperationEvent.HostStarting);
            services = host.Services.GetServices<IHostedService>().ToArray();
            await host.StartAsync(cancellationToken);
            started = true;
            using var cancellation = cancellationToken.Register(lifetime.StopApplication);
            OperationEvents.Write(logger, OperationEvent.HostStarted);
            await body(lifetime.ApplicationStopping);
        }
        catch (OperationCanceledException) when (started && lifetime.ApplicationStopping.IsCancellationRequested)
        {
            OperationEvents.Write(logger, OperationEvent.CancellationRequested);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        OperationEvents.Write(logger, OperationEvent.HostStopping);
        // Invalidate selections before worker StopAsync; observe asynchronous cancellation after workers stop.
        var selectionShutdown = companies.DisposeAsync().AsTask();
        await ObserveCleanupAsync(() => host.StopAsync(), shutdownTimeout, failures, logger);
        // BackgroundService.StopAsync may return at its cancellation deadline before ExecuteAsync finishes.
        foreach (var worker in services.OfType<BackgroundService>())
            if (worker.ExecuteTask is { } task)
                await ObserveCleanupAsync(() => task, shutdownTimeout, failures, logger, allowCancellation: true);
        await ObserveCleanupAsync(() => selectionShutdown, shutdownTimeout, failures, logger);
        OperationEvents.Write(logger, OperationEvent.HostStopped);
        try
        {
            if (host is IAsyncDisposable asynchronousHost)
                await ObserveCleanupAsync(() => asynchronousHost.DisposeAsync().AsTask(), shutdownTimeout, failures, logger);
            else host.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (runtimeFailures.Failure is { } runtimeFailure && !failures.Any(failure => ReferenceEquals(failure, runtimeFailure)))
            failures.Add(runtimeFailure);
        foreach (var failure in failures) OperationEvents.Write(logger, OperationEvent.HostFailed, failure);
        if (runtimeFailures.Failure is { } logFailure && !failures.Any(failure => ReferenceEquals(failure, logFailure)))
            failures.Add(logFailure);

        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Host execution and cleanup failed.", failures);
    }

    private static async Task ObserveCleanupAsync(Func<Task> cleanup, TimeSpan timeout, List<Exception> failures, ILogger logger,
        bool allowCancellation = false)
    {
        Task? task = null;
        try
        {
            task = cleanup();
            await task.WaitAsync(timeout);
        }
        catch (OperationCanceledException) when (allowCancellation) { }
        catch (TimeoutException exception) when (task is not null && !task.IsCompleted)
        {
            failures.Add(exception);
            OperationEvents.Write(logger, OperationEvent.ShutdownDeadlineExceeded, exception);
            // A deadline is a visible failure, not permission to dispose resources still used by a worker.
            try { await task; }
            catch (OperationCanceledException) when (allowCancellation) { }
            catch (Exception failure) { AddFailure(failures, failure); }
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }
    }

    private static void AddFailure(List<Exception> failures, Exception exception)
    {
        if (!failures.Any(failure => ReferenceEquals(failure, exception))) failures.Add(exception);
    }
}
