using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.Core.Companies;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class OperationsTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-operations-tests-" + Guid.NewGuid().ToString("N"));
    private ApplicationPaths Paths => new(new ConfigurationLocation(_root));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [TestMethod]
    public void LogsExcludeForeignMessagesFormattersScopesAndExceptionContents()
    {
        const string secret = "sentinel-credential-server-user-and-password";
        var state = new RuntimeFailureState();
        using var provider = new SafeFileLoggerProvider(Paths, state);
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var logger = factory.CreateLogger(secret);
        var failure = new InvalidOperationException(secret);
        failure.Data[secret] = secret;
        using (logger.BeginScope(secret))
            logger.Log(LogLevel.Error, new EventId(45, secret), secret, failure,
                (_, _) => throw new AssertFailedException("Foreign formatter must never run."));
        var plugin = new QuantarixLogAdapter(factory);
        plugin.LogInfo(secret);
        plugin.LogWarning(secret);
        plugin.LogError(secret, failure);
        OperationEvents.Write(logger, OperationEvent.HostStarted);
        provider.Dispose();

        var file = Directory.GetFiles(Paths.LogsDirectory).Single();
        var text = File.ReadAllText(file);
        Assert.DoesNotContain(secret, text);
        var records = File.ReadAllLines(file).Select(line => JsonDocument.Parse(line)).ToArray();
        try
        {
            Assert.HasCount(5, records);
            Assert.AreEqual("FrameworkMessage", records[0].RootElement.GetProperty("Event").GetString());
            Assert.AreEqual(nameof(InvalidOperationException), records[0].RootElement.GetProperty("ExceptionType").GetString());
            Assert.AreEqual("PluginMessage", records[1].RootElement.GetProperty("Event").GetString());
            Assert.AreEqual("HostStarted", records[4].RootElement.GetProperty("Event").GetString());
            Assert.AreEqual(TimeSpan.Zero, records[0].RootElement.GetProperty("TimestampUtc").GetDateTimeOffset().Offset);
            Assert.IsNull(state.Failure);
        }
        finally { foreach (var record in records) record.Dispose(); }
    }

    [TestMethod]
    public void LogWriteFailureAndCapacityLimitRemainObservable()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Paths.LogsDirectory, "obstacle");
        var failures = new RuntimeFailureState();
        using var blocked = new SafeFileLoggerProvider(Paths, failures);
        OperationEvents.Write(blocked.CreateLogger("test"), OperationEvent.HostStarted);
        Assert.IsInstanceOfType<OperationException>(failures.Failure);
        File.Delete(Paths.LogsDirectory);
        failures = new RuntimeFailureState();
        using var bounded = new SafeFileLoggerProvider(Paths, failures, maximumBytes: 1);
        OperationEvents.Write(bounded.CreateLogger("test"), OperationEvent.HostStarted);
        Assert.IsInstanceOfType<OperationException>(failures.Failure);
        Assert.AreEqual(0L, new FileInfo(Directory.GetFiles(Paths.LogsDirectory).Single()).Length);
    }

    [TestMethod]
    public void OperationalPathsAreSideEffectFreeAndRejectEscapesWithoutEchoingInput()
    {
        var paths = Paths;
        Assert.AreEqual(Path.Combine(_root, "recordings", "company", "call.wav"), paths.ResolveRecordingPath("company/call.wav"));
        Assert.AreEqual(Path.Combine(_root, "backups", "db.bak"), paths.ResolveBackupPath("db.bak"));
        foreach (var invalid in new[] { "../credential", "../../credential", "C:\\credential", "\\\\server\\credential", "a:credential", ".", "" })
        {
            var failure = Assert.ThrowsExactly<OperationException>(() => paths.ResolveRecordingPath(invalid));
            Assert.DoesNotContain("credential", failure.Message);
        }
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public async Task BrokenLoggingFailsTheHostWithASafeMessage()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Paths.LogsDirectory, "obstacle");
        var host = CreateBuilder().Build();
        var failure = await Assert.ThrowsExactlyAsync<OperationException>(() => HostExecution.RunAsync(host, _ =>
            throw new AssertFailedException("Body must not run without operational logging.")));
        Assert.AreEqual(failure.Message, StartupFailureMessage.Format(failure));
        Assert.DoesNotContain(_root, failure.Message);
    }

    [TestMethod]
    public async Task BackgroundFailureStopsTheHostAndPreservesTheOriginalFailure()
    {
        var failure = new InvalidOperationException("private worker error");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = CreateBuilder().ConfigureServices(services =>
            services.AddHostedService(_ => new FailingWorker(release.Task, failure))).Build();
        var run = HostExecution.RunAsync(host, async token =>
        {
            release.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
        var observed = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreSame(failure, observed);
        Assert.DoesNotContain(failure.Message, ReadLogs());
        Assert.Contains("BackgroundFailed", ReadLogs());
    }

    [TestMethod]
    public async Task ExternalCancellationAfterStartupIsGracefulAndInvalidatesCompanyBeforeWorkerStop()
    {
        using var cancellation = new CancellationTokenSource();
        var checkedStop = false;
        var host = CreateBuilder().ConfigureServices(services =>
            services.AddSingleton<IHostedService>(provider => new StopProbe(() =>
            {
                Assert.ThrowsExactly<ObjectDisposedException>(() => provider.GetRequiredService<CompanyContext>().BeginOperation());
                checkedStop = true;
            }))).Build();
        await HostExecution.RunAsync(host, async token =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, cancellation.Token);
        Assert.IsTrue(checkedStop);
        Assert.Contains("CancellationRequested", ReadLogs());
    }

    [TestMethod]
    public async Task WorkerDeadlineIsReportedButDependenciesStayAliveUntilWorkerActuallyFinishes()
    {
        var worker = new SlowWorker();
        var host = CreateBuilder().ConfigureServices(services =>
        {
            services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromMilliseconds(60));
            services.AddSingleton<IHostedService>(_ => worker);
        }).Build();
        var run = HostExecution.RunAsync(host, async _ => await worker.Started.Task);
        try
        {
            await worker.Stopping.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // Wait for the observed deadline, rather than assuming a scheduler delay is sufficient.
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!ReadLogs().Contains("ShutdownDeadlineExceeded", StringComparison.Ordinal))
                await Task.Delay(10, deadline.Token);
            Assert.IsFalse(run.IsCompleted);
            Assert.IsFalse(worker.Disposed);
        }
        finally { worker.Release.TrySetResult(); }
        var failure = await Assert.ThrowsAsync<Exception>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
        var errors = failure is AggregateException aggregate ? aggregate.Flatten().InnerExceptions.ToArray() : new[] { failure };
        Assert.IsTrue(errors.Any(error => error is TimeoutException));
        Assert.IsTrue(errors.All(error => error is TimeoutException or OperationCanceledException), string.Join("\n", errors.Select(error => error.ToString())));
        Assert.IsTrue(worker.Finished);
        Assert.IsTrue(worker.Disposed);
    }

    private string ReadLogs() => string.Join("\n", Directory.GetFiles(Paths.LogsDirectory).Select(file =>
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }));

    private IHostBuilder CreateBuilder() => WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly())
        .ConfigureServices(services =>
        {
            services.AddSingleton(new ConfigurationLocation(_root));
            services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
            services.AddSingleton<IHostLifetime, TestLifetime>();
        });

    private sealed class TestLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailingWorker(Task release, Exception failure) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await release;
            throw failure;
        }
    }

    private sealed class StopProbe(Action stop) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) { stop(); return Task.CompletedTask; }
    }

    private sealed class SlowWorker : BackgroundService
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Stopping { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Finished { get; private set; }
        internal bool Disposed { get; private set; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Started.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            Stopping.SetResult();
            await Release.Task;
            Assert.IsFalse(Disposed, "Worker must finish while its dependencies are still usable.");
            Finished = true;
        }

        public override void Dispose() { Disposed = true; base.Dispose(); }
    }
}
