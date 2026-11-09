using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quantarix.Plugin.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Plugins;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Core.Companies;
using System.Text.Json.Nodes;

namespace WawiCallPro.Utilities.Tests;

[TestClass, DoNotParallelize]
public sealed class PluginRuntimeTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-plugin-runtime-" + Guid.NewGuid().ToString("N"));
    [TestCleanup]
    public async Task Cleanup()
    {
        // Quantarix requests collectible-ALC unloading; Windows releases mapped DLLs after collection.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers();
            await Task.Delay(20);
        }
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [TestMethod]
    public async Task SharedHostFactoryCreatesCallerOwnedQuantarixManagersWithoutStartupDiscovery()
    {
        var host = CreateHost();
        var factory = host.Services.GetRequiredService<IPluginManagerFactory>();
        Assert.AreSame(factory, host.Services.GetRequiredService<IPluginManagerFactory>());
        Assert.IsFalse(Directory.Exists(_root));
        await HostExecution.RunAsync(host, _ =>
        {
            using var first = factory.Create(new HashSet<string>());
            using var second = factory.Create(new HashSet<string>());
            Assert.IsInstanceOfType<PluginManager>(first);
            Assert.AreNotSame(first, second);
            Assert.AreSame(host.Services.GetRequiredService<Quantarix.Plugin.Abstractions.ILogger>(), first.Logger);
            Assert.AreEqual(0, first.Plugins.Count);
            Assert.ThrowsExactly<ArgumentNullException>(() => factory.Create(null!));
            return Task.CompletedTask;
        });
        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "plugins")));
        Assert.IsFalse(File.Exists(Path.Combine(_root, "communications.db")));
    }

    [TestMethod]
    public async Task RealQuantarixRuntimeLoadsExecutesAndShutsDownIsolatedFixtureWithSafeLogging()
    {
        var plugins = DeployFixture(); var host = CreateHost();
        await HostExecution.RunAsync(host, _ =>
        {
            using var manager = host.Services.GetRequiredService<IPluginManagerFactory>().Create(new HashSet<string>());
            manager.LoadFromDirectory(plugins);
            Assert.AreEqual(1, manager.Plugins.Count);
            var plugin = manager.Plugins.Single();
            var company = new CompanyKey(LexwareProductLine.Pro, "shared", "0001");
            var echo = plugin.GetType().GetMethod("EchoCompany")!;
            Assert.AreSame(typeof(CompanyKey), echo.GetParameters()[0].ParameterType);
            Assert.AreSame(company, echo.Invoke(plugin, [company]));
            var collection = new ServiceCollection();
            Assert.AreSame(collection, plugin.GetType().GetMethod("EchoServices")!.Invoke(plugin, [collection]));
            Assert.AreSame(typeof(Dapper.SqlMapper).Assembly, plugin.GetType().GetProperty("DapperAssembly")!.GetValue(plugin));
            Assert.AreEqual(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(typeof(CompanyKey).Assembly));
            Assert.AreEqual("private-plugin-sentinel", plugin.Name);
            Assert.AreEqual(new Version(1, 0), plugin.Version);
            Assert.IsTrue(AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly)!.IsCollectible);
            manager.ExecuteAll(); manager.UnloadAll();
            Assert.AreEqual(1, plugin.GetType().GetProperty("Initializations")!.GetValue(plugin));
            Assert.AreEqual(1, plugin.GetType().GetProperty("Executions")!.GetValue(plugin));
            Assert.AreEqual(1, plugin.GetType().GetProperty("Shutdowns")!.GetValue(plugin));
            Assert.AreEqual(0, manager.Plugins.Count);
            return Task.CompletedTask;
        });
        var logs = string.Join('\n', Directory.GetFiles(Path.Combine(_root, "logs")).Select(File.ReadAllText));
        Assert.DoesNotContain("private-plugin-sentinel", logs);
        Assert.IsTrue(logs.Split("PluginMessage", StringSplitOptions.None).Length >= 5);
    }

    [TestMethod]
    public async Task SelectedFolderLoadsOnlyItsPluginAndKeepsSignatureChecks()
    {
        var plugins = DeployFixture();
        Directory.CreateDirectory(Path.Combine(plugins, "BrokenSibling"));
        var host = CreateHost();
        await HostExecution.RunAsync(host, _ =>
        {
            var factory = host.Services.GetRequiredService<IPluginManagerFactory>();
            using var selected = factory.Create(new HashSet<string>());
            selected.LoadSelectedDirectory(Path.Combine(plugins, "RuntimePlugin"));
            Assert.AreEqual(1, selected.Plugins.Count);
            Assert.AreEqual(0, selected.LoadFailureCount);
            using var rejected = factory.Create(new HashSet<string> { "0000000000000000" });
            rejected.LoadSelectedDirectory(Path.Combine(plugins, "RuntimePlugin"));
            Assert.AreEqual(0, rejected.Plugins.Count);
            Assert.AreEqual(1, rejected.LoadFailureCount);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task CallerCannotDisableManagerTokenCheckByMutatingItsOriginalSet()
    {
        var plugins = DeployFixture(); var host = CreateHost();
        await HostExecution.RunAsync(host, _ =>
        {
            var tokens = new HashSet<string> { "0000000000000000" };
            using var manager = host.Services.GetRequiredService<IPluginManagerFactory>().Create(tokens);
            tokens.Clear();
            manager.LoadFromDirectory(plugins);
            Assert.AreEqual(0, manager.Plugins.Count);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task PluginSessionUsesConstructorInjectionAndConfiguresBorrowedHostServices()
    {
        var plugins = DeployFixture(); var host = CreateHost();
        await HostExecution.RunAsync(host, _ =>
        {
            using var session = host.Services.GetRequiredService<IPluginSessionFactory>().Create(new HashSet<string>());
            session.Load(plugins);
            var plugin = session.GetPlugins().Single();
            Assert.AreEqual(true, plugin.GetType().GetProperty("ServicesInjected")!.GetValue(plugin));
            Assert.AreEqual(true, plugin.GetType().GetProperty("Configured")!.GetValue(plugin));
            Assert.AreEqual(true, plugin.GetType().GetProperty("PrivateServiceInjected")!.GetValue(plugin));
            bool? disposedAtUnload = null;
            var context = AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly)!;
            Action<AssemblyLoadContext> unloading = _ =>
                disposedAtUnload = (bool)plugin.GetType().GetProperty("PrivateServiceDisposed")!.GetValue(plugin)!;
            context.Unloading += unloading;
            session.Dispose();
            context.Unloading -= unloading;
            Assert.AreEqual(true, disposedAtUnload);
            Assert.AreEqual(true, plugin.GetType().GetProperty("PrivateServiceDisposed")!.GetValue(plugin));
            Assert.AreEqual(true, plugin.GetType().GetProperty("PrivateServiceDisposedAfterShutdown")!.GetValue(plugin));
            Assert.IsNotNull(host.Services.GetRequiredService<IWawiCallHostServices>().Companies.Current);
            // MSTest retains completed task/closure state through cleanup; release the observed plugin.
            plugin = null!;
            context = null!;
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task PluginOwnedScopedServicesAreAwaitedBeforeContextRelease()
    {
        var plugins = DeploySignedFixture(); var host = CreateHost();
        await HostExecution.RunAsync(host, async _ =>
        {
            await using var session = host.Services.GetRequiredService<IPluginSessionFactory>().CreateTrusted();
            await session.LoadAsync(plugins);
            var plugin = session.GetPlugins().Single();
            Assert.AreEqual(true, plugin.GetType().GetProperty("PrivateServiceInjected")!.GetValue(plugin));
            bool? disposedAtUnload = null;
            var context = AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly)!;
            Action<AssemblyLoadContext> unloading = _ =>
                disposedAtUnload = (bool)plugin.GetType().GetProperty("PrivateServiceDisposedAsynchronously")!.GetValue(plugin)!;
            context.Unloading += unloading;
            await session.DisposeAsync();
            context.Unloading -= unloading;
            Assert.AreEqual(true, disposedAtUnload);
            Assert.AreEqual(true, plugin.GetType().GetProperty("PrivateServiceDisposedAsynchronously")!.GetValue(plugin));
            Assert.AreEqual(true, plugin.GetType().GetProperty("PrivateServiceDisposedAfterShutdown")!.GetValue(plugin));
            Assert.IsNotNull(host.Services.GetRequiredService<IWawiCallHostServices>().Companies.Current);
            plugin = null!;
            context = null!;
        });
        // Leave the async service-disposal continuation before cleanup collects its load context.
        await Task.Delay(20);
    }

    [TestMethod]
    public async Task ProductionTrustComesFromSignedHostContractAndRejectsUnsignedFixtures()
    {
        var plugins = DeployFixture(); var host = CreateHost();
        await HostExecution.RunAsync(host, _ =>
        {
            var trust = host.Services.GetRequiredService<IPluginTrust>().GetAllowedTokens();
            Assert.AreEqual(1, trust.Count);
            Assert.AreEqual(16, trust.Single().Length);
            using var session = host.Services.GetRequiredService<IPluginSessionFactory>().CreateTrusted();
            Assert.ThrowsExactly<PluginRuntimeException>(() => session.Load(plugins));
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task RuntimeCancelsAndAwaitsWorkClosesResourcesAndReleasesCollectibleContext()
    {
        var context = await RunRuntimeScenarioAsync();
        // Leave the inline async-stop continuation stack before testing collectible-ALC reclamation.
        for (var index = 0; index < 20 && context.IsAlive; index++)
        {
            await Task.Delay(20);
            GC.Collect(); GC.WaitForPendingFinalizers();
        }
        Assert.IsFalse(context.IsAlive, "Plugin context remains rooted after shutdown.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task<WeakReference> RunRuntimeScenarioAsync()
    {
        var directory = DeploySignedFixture();
        var host = WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly()).ConfigureServices(services =>
        {
            services.AddSingleton(new ConfigurationLocation(_root));
            services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
            services.AddProductPluginRuntime();
            services.AddSingleton(new ProductPluginLocation(directory));
        }).Build();
        WeakReference? context = null;
        await HostExecution.RunAsync(host, async token =>
        {
            var runtime = host.Services.GetRequiredService<IProductPluginRuntime>();
            Assert.IsTrue(runtime.IsLoaded);
            context = await runtime.UseAsync((plugin, _) => Task.FromResult(new WeakReference(AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly)!)), token);
            var resource = new ProbeResource();
            await runtime.UseAsync<IPluginLifetimeResource>((_, _) => Task.FromResult<IPluginLifetimeResource>(resource), token);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var work = runtime.UseAsync(async (_, cancellation) => { entered.SetResult(); await Task.Delay(Timeout.Infinite, cancellation); return 0; }, token);
            await entered.Task;
            await host.Services.GetRequiredService<ProductPluginRuntime>().StopAsync(CancellationToken.None);
            await Assert.ThrowsAsync<OperationCanceledException>(() => work);
            Assert.IsTrue(resource.Disposed);
            Assert.IsFalse(runtime.IsLoaded);
        });
        return context!;
    }
    private sealed class ProbeResource : IPluginLifetimeResource
    {
        public bool Disposed { get; private set; }
        public bool Fail { get; set; }
        public ValueTask DisposeAsync() { if (Fail) throw new IOException("synthetic failure"); Disposed = true; return ValueTask.CompletedTask; }
    }

    [TestMethod]
    public async Task CompanySwitchCancelsQueuedWorkAndRetainsRunningResourcesUntilStopCompletes()
    {
        var context = await RunCompanySwitchScenarioAsync();
        // Let shutdown continuations leave the stack before asserting collectible context reclamation.
        for (var index = 0; index < 20 && context.IsAlive; index++)
        {
            await Task.Delay(20);
            GC.Collect(); GC.WaitForPendingFinalizers();
        }
        Assert.IsFalse(context.IsAlive, "Company operation retained the unloaded plugin context.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task<WeakReference> RunCompanySwitchScenarioAsync()
    {
        var directory = DeploySignedFixture();
        var host = CreateHost();
        WeakReference? pluginContext = null;
        await HostExecution.RunAsync(host, async _ =>
        {
            var context = host.Services.GetRequiredService<CompanyContext>();
            var borrowed = host.Services.GetRequiredService<IWawiCallHostServices>();
            var product = Enum.Parse<LexwareProductLine>(ApplicationIdentity.GetProductLine(Assembly.GetExecutingAssembly()));
            await context.SelectAsync(new Company(new CompanyKey(product, "isolated", "1"), "First"));
            using var operation = borrowed.Companies.BeginOperation();
            await using var runtime = new ProductPluginRuntime(host.Services.GetRequiredService<IPluginSessionFactory>(), new ProductPluginLocation(directory));
            await runtime.StartAsync(CancellationToken.None);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<ProbeResource>(TaskCreationOptions.RunContinuationsAsynchronously);
            var resource = new ProbeResource();
            IWawiCallPlugin? runningPlugin = null;
            var work = operation.LoadAndPublishAsync(borrowed.UiDispatcher, (company, token) => runtime.UseAsync((plugin, _) =>
            {
                Assert.AreEqual("1", company.ExternalId);
                runningPlugin = plugin;
                pluginContext = new WeakReference(AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly)!);
                entered.SetResult();
                return release.Task;
            }, token), _ => Assert.Fail("Stale result published."), _ => Assert.Fail("Cancellation became an error view."));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var queued = runtime.UseAsync((_, _) => { Assert.Fail("Cancelled queued work entered the plugin."); return Task.FromResult(0); }, operation.CancellationToken);
            Task? stop = null;
            try
            {
                await context.SelectAsync(new Company(new CompanyKey(product, "isolated", "2"), "Second"));
                await Assert.ThrowsAsync<OperationCanceledException>(() => queued);
                Assert.IsFalse(work.IsCompleted);
                using var deadline = new CancellationTokenSource();
                deadline.Cancel();
                stop = runtime.StopAsync(deadline.Token);
                Assert.IsFalse(stop.IsCompleted);
                Assert.IsFalse(resource.Disposed);
                Assert.AreEqual(0, runningPlugin!.GetType().GetProperty("Shutdowns")!.GetValue(runningPlugin));
            }
            finally { release.TrySetResult(resource); }

            await Assert.ThrowsAsync<OperationCanceledException>(() => work);
            await stop!.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(resource.Disposed, "Cancelled returned resources must remain owned by the runtime.");
            Assert.AreEqual(1, runningPlugin!.GetType().GetProperty("Shutdowns")!.GetValue(runningPlugin));
            runningPlugin = null;
            // Completed task state must not keep the collectible fixture alive through MSTest cleanup.
            work = null!;
            queued = null!;
            stop = null;
        });
        return pluginContext!;
    }

    private string DeploySignedFixture([CallerFilePath] string source = "")
    {
        var variant = ApplicationIdentity.GetProductLine(Assembly.GetExecutingAssembly());
        var configuration = typeof(PluginRuntimeTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var output = Path.Combine(Path.GetDirectoryName(source)!, "Fixtures", "SignedRuntimePlugin", "bin", configuration, variant, "net10.0");
        var plugins = Path.Combine(_root, "plugins"); var target = Path.Combine(plugins, "SignedRuntimePlugin");
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(output)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        AddNativeProbe(target);
        return plugins;
    }

    [TestMethod]
    [DataRow("missing-directory")]
    [DataRow("missing-dll")]
    [DataRow("bad-image")]
    [DataRow("wrong-dll")]
    [DataRow("wrong-token")]
    [DataRow("constructor")]
    [DataRow("initialize")]
    [DataRow("configure")]
    [DataRow("missing-dependency")]
    [DataRow("mixed-good-bad")]
    [DataRow("wrong-product")]
    public async Task LoadFailuresRejectTheSessionAndKeepPrivateValuesOutOfErrors(string fault)
    {
        var directory = DeploySignedFixture(); var bundle = Path.Combine(directory, "SignedRuntimePlugin");
        if (fault == "missing-directory") directory = Path.Combine(_root, "absent");
        if (fault == "missing-dll") File.Delete(Path.Combine(bundle, "SignedRuntimePlugin.dll"));
        if (fault == "bad-image") File.WriteAllText(Path.Combine(bundle, "SignedRuntimePlugin.dll"), "not an assembly");
        if (fault == "wrong-dll") File.Copy(Path.Combine(bundle, "PrivateDependency.dll"), Path.Combine(bundle, "SignedRuntimePlugin.dll"), overwrite: true);
        if (fault == "missing-dependency") File.Delete(Path.Combine(bundle, "PrivateDependency.dll"));
        if (fault == "mixed-good-bad") { var bad = Path.Combine(directory, "bad"); System.IO.Directory.CreateDirectory(bad); File.WriteAllText(Path.Combine(bad, "bad.dll"), "bad"); }
        var previous = Environment.GetEnvironmentVariable("WCP_PLUGIN_FIXTURE_FAULT");
        Environment.SetEnvironmentVariable("WCP_PLUGIN_FIXTURE_FAULT", fault);
        try
        {
            var host = CreateHost();
            await HostExecution.RunAsync(host, async _ =>
            {
                var factory = host.Services.GetRequiredService<IPluginSessionFactory>();
                await using var session = fault == "wrong-token" ? factory.Create(new HashSet<string> { "0000000000000000" }) : factory.CreateTrusted();
                var failure = await Assert.ThrowsExactlyAsync<PluginRuntimeException>(() => session.LoadAsync(directory));
                Assert.DoesNotContain("fixture-private-secret", failure.ToString());
            });
        }
        finally { Environment.SetEnvironmentVariable("WCP_PLUGIN_FIXTURE_FAULT", previous); }
        var logs = string.Join('\n', System.IO.Directory.GetFiles(Path.Combine(_root, "logs")).Select(File.ReadAllText));
        Assert.DoesNotContain("fixture-private-secret", logs);
    }

    [TestMethod]
    public async Task ShutdownFailureIsVisibleAfterScopesAreReleased()
    {
        var directory = DeploySignedFixture(); var host = CreateHost();
        await HostExecution.RunAsync(host, async _ =>
        {
            await using var session = host.Services.GetRequiredService<IPluginSessionFactory>().CreateTrusted();
            await session.LoadAsync(directory);
            var previous = Environment.GetEnvironmentVariable("WCP_PLUGIN_FIXTURE_FAULT");
            try
            {
                Environment.SetEnvironmentVariable("WCP_PLUGIN_FIXTURE_FAULT", "shutdown");
                await Assert.ThrowsExactlyAsync<PluginRuntimeException>(() => session.DisposeAsync().AsTask());
            }
            finally { Environment.SetEnvironmentVariable("WCP_PLUGIN_FIXTURE_FAULT", previous); }
        });
    }

    [TestMethod]
    public async Task FailedAsyncStopRemainsVisibleAndCanFinishBeforeUnloading()
    {
        var directory = DeploySignedFixture();
        var host = WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly()).ConfigureServices(services =>
        {
            services.AddSingleton(new ConfigurationLocation(_root));
            services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
            services.AddProductPluginRuntime(); services.AddSingleton(new ProductPluginLocation(directory));
        }).Build();
        await HostExecution.RunAsync(host, async _ =>
        {
            var runtime = host.Services.GetRequiredService<ProductPluginRuntime>();
            var previous = Environment.GetEnvironmentVariable("WCP_PLUGIN_FIXTURE_FAULT");
            try
            {
                Environment.SetEnvironmentVariable("WCP_PLUGIN_FIXTURE_FAULT", "stop");
                await Assert.ThrowsExactlyAsync<PluginRuntimeException>(() => runtime.StopAsync(CancellationToken.None));
                Assert.IsFalse(runtime.IsLoaded);
            }
            finally { Environment.SetEnvironmentVariable("WCP_PLUGIN_FIXTURE_FAULT", previous); }
            await runtime.StopAsync(CancellationToken.None);
        });
    }

    [TestMethod]
    public async Task ConcurrentOrRepeatedStartsKeepExactlyOneOwnedPluginSession()
    {
        var directory = DeploySignedFixture();
        var host = CreateHost();
        await HostExecution.RunAsync(host, async _ =>
        {
            var sessions = new CountingSessions(host.Services.GetRequiredService<IPluginSessionFactory>());
            await using var runtime = new ProductPluginRuntime(sessions, new ProductPluginLocation(directory));
            var starts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                try { await runtime.StartAsync(CancellationToken.None); return true; }
                catch (InvalidOperationException) { return false; }
            }));
            Assert.AreEqual(1, starts.Count(started => started));
            Assert.AreEqual(1, sessions.Created);
            var original = await runtime.UseAsync((plugin, _) => Task.FromResult(plugin));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => runtime.StartAsync(CancellationToken.None));
            Assert.AreSame(original, await runtime.UseAsync((plugin, _) => Task.FromResult(plugin)));
            Assert.IsTrue(runtime.IsLoaded);
            await runtime.StopAsync(CancellationToken.None);
            Assert.IsFalse(runtime.IsLoaded);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => runtime.StartAsync(CancellationToken.None));
            Assert.AreEqual(1, sessions.Created);
        });
    }

    [TestMethod]
    public async Task StoppedRuntimeCannotCreateItsFirstPluginSessionLater()
    {
        var host = CreateHost();
        await HostExecution.RunAsync(host, async _ =>
        {
            var sessions = new CountingSessions(host.Services.GetRequiredService<IPluginSessionFactory>());
            await using var runtime = new ProductPluginRuntime(sessions, new ProductPluginLocation(_root));
            await runtime.StopAsync(CancellationToken.None);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => runtime.StartAsync(CancellationToken.None));
            Assert.AreEqual(0, sessions.Created);
        });
    }

    [TestMethod]
    public async Task StopDuringLoadingAwaitsAndReleasesTheOnlySession()
    {
        var sessions = new LoadingSession();
        await using var runtime = new ProductPluginRuntime(sessions, new ProductPluginLocation(_root));
        var start = runtime.StartAsync(CancellationToken.None);
        await sessions.Entered.Task;
        var stop = runtime.StopAsync(CancellationToken.None);
        Assert.IsFalse(stop.IsCompleted);
        sessions.Continue.SetResult();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => start);
        await stop;
        Assert.AreEqual(1, sessions.Disposed);
        Assert.IsFalse(runtime.IsLoaded);
    }

    private sealed class LoadingSession : IPluginSessionFactory, IPluginSession
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Disposed { get; private set; }
        public IPluginSession Create(IReadOnlySet<string> tokens) => this;
        public IPluginSession CreateTrusted() => this;
        public void Load(string absoluteDirectory) => throw new NotSupportedException();
        public async Task LoadAsync(string absoluteDirectory) { Entered.SetResult(); await Continue.Task; }
        public IReadOnlyList<IWawiCallPlugin> GetPlugins() => [];
        public void Dispose() => Disposed++;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class CountingSessions(IPluginSessionFactory inner) : IPluginSessionFactory
    {
        private int _created;
        public int Created => _created;
        public IPluginSession Create(IReadOnlySet<string> tokens) { Interlocked.Increment(ref _created); return inner.Create(tokens); }
        public IPluginSession CreateTrusted() { Interlocked.Increment(ref _created); return inner.CreateTrusted(); }
    }

    [TestMethod]
    public async Task RealDummySwitchWaitsForOldCallsDisposesResourcesAndCanSwitchBack()
    {
        var directory = DeploySignedFixture();
        var variant = ApplicationIdentity.GetProductLine(Assembly.GetExecutingAssembly());
        var config = typeof(PluginRuntimeTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        // The repository root is located without depending on the test runner's working directory.
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "WawiCallProSolution.sln"))) repository = repository.Parent;
        Assert.IsNotNull(repository);
        var source = Path.Combine(repository.FullName, "WawiCallPro.Plugins.Dummy", "bin", config, "net10.0");
        var target = Path.Combine(directory, "WawiCallPro.Plugins.Dummy");
        Directory.CreateDirectory(target);
        foreach (var file in System.IO.Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        var host = CreateHost();
        await HostExecution.RunAsync(host, async _ =>
        {
            await using var runtime = new ProductPluginRuntime(host.Services.GetRequiredService<IPluginSessionFactory>(), new ProductPluginLocation(directory));
            await runtime.StartAsync(default);
            var originalId = runtime.ActivePluginId;
            Assert.AreEqual(2, runtime.GetAvailablePlugins().Count);
            var resource = new SwitchResource();
            await runtime.UseAsync((_, _) => Task.FromResult(resource));
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var oldCall = runtime.UseAsync(async (_, token) => { entered.SetResult(); await release.Task; return 42; });
            await entered.Task;
            var switching = runtime.SwitchAsync("WawiCallPro.Plugins.Dummy");
            Assert.IsFalse(switching.IsCompleted);
            release.SetResult();
            await Assert.ThrowsAsync<OperationCanceledException>(() => oldCall);
            await switching;
            Assert.IsTrue(resource.Disposed);
            Assert.AreEqual("WawiCallPro.Plugins.Dummy", runtime.ActivePluginId);
            var companies = await runtime.UseAsync((plugin, token) => plugin.Directory.GetCompaniesAsync(token));
            Assert.AreEqual("Dummy – Testmandant", companies.Single().DisplayName);
            await runtime.SwitchAsync(originalId!);
            Assert.AreEqual(originalId, runtime.ActivePluginId);
            Assert.IsTrue(runtime.IsLoaded);
            await Assert.ThrowsExactlyAsync<PluginRuntimeException>(() => runtime.SwitchAsync("../outside"));
            Assert.IsTrue(runtime.IsLoaded);
            await runtime.StopAsync(default);
            var store = host.Services.GetRequiredService<ConfigurationStore>();
            var snapshot = await store.LoadAsync();
            await store.SaveAsync(snapshot.Options with { User = snapshot.Options.User with { ActivePluginId = "WawiCallPro.Plugins.Dummy" } }, snapshot);
            var startup = host.Services.GetRequiredService<ApplicationConfigurationService>();
            await startup.StartAsync(default);
            await using var restarted = new ProductPluginRuntime(host.Services.GetRequiredService<IPluginSessionFactory>(), new ProductPluginLocation(directory), startup);
            await restarted.StartAsync(default);
            Assert.AreEqual("WawiCallPro.Plugins.Dummy", restarted.ActivePluginId);
            var failingResource = new SwitchResource { Fail = true };
            await restarted.UseAsync((_, _) => Task.FromResult(failingResource));
            await Assert.ThrowsExactlyAsync<PluginRuntimeException>(() => restarted.SwitchAsync(originalId!));
            Assert.IsFalse(restarted.IsLoaded);
            Assert.IsNull(restarted.ActivePluginId);
            failingResource.Fail = false;
            await Assert.ThrowsExactlyAsync<PluginRuntimeException>(() => restarted.SwitchAsync(originalId!));
            await restarted.StopAsync(default);
        });
    }

    private sealed class SwitchResource : IPluginLifetimeResource
    {
        public bool Disposed { get; private set; }
        public bool Fail { get; set; }
        public ValueTask DisposeAsync() { if (Fail) throw new IOException("synthetic failure"); Disposed = true; return ValueTask.CompletedTask; }
    }

    private IHost CreateHost() => WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly()).ConfigureServices(services =>
    {
        services.AddSingleton(new ConfigurationLocation(_root));
        services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
    }).Build();

    private string DeployFixture([CallerFilePath] string source = "")
    {
        var variant = ApplicationIdentity.GetProductLine(Assembly.GetExecutingAssembly());
        var configuration = typeof(PluginRuntimeTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var output = Path.Combine(Path.GetDirectoryName(source)!, "Fixtures", "RuntimePlugin", "bin", configuration, variant, "net10.0");
        var plugins = Path.Combine(_root, "plugins"); var destination = Path.Combine(plugins, "RuntimePlugin");
        Directory.CreateDirectory(destination);
        // Only the fixture and its dependency manifest; abstractions must resolve to the host's loaded contract.
        foreach (var name in new[] { "RuntimePlugin.dll", "RuntimePlugin.deps.json", "WawiCallPro.Interfaces.dll", "Quantarix.Plugin.Abstractions.dll", "Dapper.dll", "Microsoft.Extensions.DependencyInjection.Abstractions.dll", "PrivateDependency.dll" }) File.Copy(Path.Combine(output, name), Path.Combine(destination, name));
        return plugins;
    }
    private static void AddNativeProbe(string target)
    {
        var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
        var relative = "runtimes/" + rid + "/native/wcp-native-probe.dll";
        var file = Path.Combine(target, relative); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.Copy(Path.Combine(Environment.SystemDirectory, "version.dll"), file);
        var manifest = Path.Combine(target, "SignedRuntimePlugin.deps.json");
        var json = JsonNode.Parse(File.ReadAllText(manifest))!;
        var framework = json["runtimeTarget"]!["name"]!.GetValue<string>();
        var dependency = json["targets"]![framework]!.AsObject().First(item => item.Key.StartsWith("PrivateDependency/", StringComparison.Ordinal)).Value!;
        dependency["runtimeTargets"] = new JsonObject { [relative] = new JsonObject { ["rid"] = rid, ["assetType"] = "native" } };
        File.WriteAllText(manifest, json.ToJsonString());
    }
}
