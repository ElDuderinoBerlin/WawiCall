using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.Core.Companies;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class HostingTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-host-tests-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private IHostBuilder CreateBuilder() => WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly())
        .ConfigureServices(services =>
        {
            services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
            services.AddSingleton<IHostLifetime, TestHostLifetime>();
            services.AddSingleton(new ConfigurationLocation(_root));
        });

    [TestMethod]
    public async Task HostUsesOneCompanyContextAndReportsItsActualIdentity()
    {
        var host = CreateBuilder().Build();
        CompanyContext? context = null;
        await HostExecution.RunAsync(host, async stoppingToken =>
        {
            context = host.Services.GetRequiredService<CompanyContext>();
            Assert.AreSame(context, host.Services.GetRequiredService<ICompanyContext>());
            var report = await host.Services.GetRequiredService<HostDiagnostics>().CaptureAsync(stoppingToken);
            Assert.AreEqual(ApplicationIdentity.GetProductLine(Assembly.GetExecutingAssembly()), report.ProductLine);
            Assert.AreEqual("WawiCallPro", report.Application);
            Assert.IsTrue(report.HostStarted);
            Assert.IsFalse(report.ConnectionConfigured);
            Assert.IsFalse(report.CompanySelected);
            Assert.AreEqual(nameof(InlineUiDispatcher), report.Dispatcher);
        });
        Assert.ThrowsExactly<ObjectDisposedException>(() => context!.BeginOperation());
    }

    [TestMethod]
    public async Task UnimplementedBusinessServicesAreNotRegisteredAsSuccessfulStubs()
    {
        var host = CreateBuilder().Build();
        await HostExecution.RunAsync(host, _ =>
        {
            Assert.IsNull(host.Services.GetService<IBusinessDirectory>());
            Assert.IsNull(host.Services.GetService<ICallHistoryService>());
            Assert.IsNull(host.Services.GetService<ITelephonyService>());
            Assert.IsNull(host.Services.GetService<IWawiCallHostServices>());
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public void MissingDispatcherFailsAtBuildRatherThanAtFirstBusinessOperation()
    {
        Assert.ThrowsExactly<AggregateException>(() => WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly()).Build());
    }

    [TestMethod]
    public void ScopedDependencyCannotBeCapturedByASingleton()
    {
        var builder = CreateBuilder().ConfigureServices(services =>
        {
            services.AddScoped<ScopedDependency>();
            services.AddSingleton<InvalidSingleton>();
        });
        Assert.ThrowsExactly<AggregateException>(() => builder.Build());
    }

    [TestMethod]
    public async Task FailedStartupStillStopsInReverseOrderAndDisposesTheContainer()
    {
        var events = new List<string>();
        var failure = new InvalidOperationException("startup failed");
        var host = CreateBuilder().ConfigureServices(services =>
        {
            services.AddSingleton<IHostedService>(_ => new ProbeService("first", events));
            services.AddSingleton<IHostedService>(_ => new ProbeService("second", events, startFailure: failure));
        }).Build();
        var invoked = false;
        var observed = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => HostExecution.RunAsync(host, _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        }));

        Assert.AreSame(failure, observed);
        Assert.IsFalse(invoked);
        CollectionAssert.AreEqual(new[] { "start:first", "start:second", "stop:second", "stop:first", "dispose:second", "dispose:first" }, events);
    }

    [TestMethod]
    public async Task WorkerFinishesBeforeHostExecutionReturns()
    {
        var worker = new CancellationWorker();
        var host = CreateBuilder().ConfigureServices(services =>
            services.AddSingleton<IHostedService>(_ => worker)).Build();
        await HostExecution.RunAsync(host, async _ => await worker.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(worker.Finished.Task.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task BodyStopAndDisposalFailuresRemainObservableTogether()
    {
        var bodyFailure = new InvalidOperationException("body failed");
        var stopFailure = new InvalidOperationException("stop failed");
        var disposeFailure = new InvalidOperationException("dispose failed");
        var host = CreateBuilder().ConfigureServices(services => services.AddSingleton<IHostedService>(_ =>
            new ProbeService("probe", [], stopFailure: stopFailure, disposeFailure: disposeFailure))).Build();
        var failure = await Assert.ThrowsExactlyAsync<AggregateException>(() =>
            HostExecution.RunAsync(host, _ => Task.FromException(bodyFailure)));
        var errors = failure.Flatten().InnerExceptions;
        Assert.Contains(bodyFailure, errors);
        Assert.Contains(stopFailure, errors);
        Assert.Contains(disposeFailure, errors);
    }

    [TestMethod]
    public async Task CancelledStartupDoesNotSkipCleanup()
    {
        var host = CreateBuilder().Build();
        var context = host.Services.GetRequiredService<CompanyContext>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            HostExecution.RunAsync(host, _ => Task.CompletedTask, cancellation.Token));
        Assert.ThrowsExactly<ObjectDisposedException>(() => context.BeginOperation());
    }

    private sealed class TestHostLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ScopedDependency;
    private sealed class InvalidSingleton(ScopedDependency dependency)
    {
        public ScopedDependency Dependency { get; } = dependency;
    }

    private sealed class ProbeService(string name, List<string> events, Exception? startFailure = null,
        Exception? stopFailure = null, Exception? disposeFailure = null) : IHostedService, IAsyncDisposable
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            events.Add($"start:{name}");
            return startFailure is null ? Task.CompletedTask : Task.FromException(startFailure);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            events.Add($"stop:{name}");
            return stopFailure is null ? Task.CompletedTask : Task.FromException(stopFailure);
        }

        public ValueTask DisposeAsync()
        {
            events.Add($"dispose:{name}");
            return disposeFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(disposeFailure);
        }
    }

    private sealed class CancellationWorker : BackgroundService
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Started.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            finally { Finished.SetResult(); }
        }
    }
}
