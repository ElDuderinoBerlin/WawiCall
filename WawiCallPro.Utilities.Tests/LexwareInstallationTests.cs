using Microsoft.Extensions.Logging;
using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class LexwareInstallationTests
{
    [TestMethod]
    [DataRow(false, false, 0)]
    [DataRow(true, false, 0)]
    [DataRow(false, true, 0)]
    [DataRow(true, true, 0)]
    [DataRow(false, true, 1)]
    [DataRow(true, false, 2)]
    [DataRow(false, false, 3)]
    public void BothRegistryChecksAlwaysRunAndAnyCheckFailureKeepsTheGateClosed(bool pro, bool fin, int failure)
    {
        var registry = new RegistryProbe { Pro = pro, FinOffice = fin, Failure = failure };
        var log = new CaptureLog();
        var installation = new LexwareInstallation(registry, log);
        var state = installation.Check();
        Assert.AreEqual(1, registry.ProChecks);
        Assert.AreEqual(1, registry.FinChecks);
        Assert.AreEqual(pro, state.ProInstalled);
        Assert.AreEqual(fin, state.FinOfficeInstalled);
        Assert.AreEqual(failure != 0, state.CheckFailed);
        Assert.AreEqual(failure == 0 && (pro || fin), state.IsAvailable);
        if (state.IsAvailable) { installation.EnsureAvailable(); Assert.IsEmpty(log.Events); }
        else
        {
            var error = Assert.ThrowsExactly<LexwareUnavailableException>(installation.EnsureAvailable);
            Assert.IsFalse(error.ToString().Contains("synthetic-sensitive-value"));
            Assert.HasCount(1, log.Events, "Unchanged unavailable states must not flood the log on every background retry.");
            Assert.AreEqual(LogLevel.Warning, log.Events[0].Level);
            Assert.AreEqual(failure == 0 ? OperationEvent.LexwareMissing : OperationEvent.LexwareCheckFailed, log.Events[0].Event);
        }
    }

    [TestMethod]
    [DataRow("WawiCallPro.Plugins.Lexware.Pro", true, false)]
    [DataRow("WawiCallPro.Plugins.Lexware.Pro", false, true)]
    [DataRow("WawiCallPro.Plugins.Lexware.FinOffice", true, false)]
    [DataRow("WawiCallPro.Plugins.Lexware.FinOffice", false, true)]
    public async Task EitherInstallationAllowsEachEditionWithoutChangingTheSelectedPlugin(string pluginId, bool pro, bool fin)
    {
        using var fixture = new Fixture(pluginId);
        var registry = new RegistryProbe { Pro = pro, FinOffice = fin };
        var installation = new LexwareInstallation(registry, new CaptureLog());
        await using var runtime = new ProductPluginRuntime(fixture.Sessions, new(fixture.Root), installation: installation);
        await runtime.StartAsync(default);
        Assert.IsTrue(runtime.CanUseActivePlugin);
        Assert.AreEqual(42, await runtime.UseAsync((_, _) => Task.FromResult(42)));
        Assert.AreEqual(pluginId, runtime.ActivePluginId);
        Assert.AreEqual(pluginId, Path.GetFileName(fixture.Sessions.LastLoaded));
        Assert.IsTrue(registry.ProChecks >= 2, "Source execution must recheck the startup prerequisite.");
        Assert.AreEqual(registry.ProChecks, registry.FinChecks);
    }

    [TestMethod]
    public async Task StartupAndDirectCallsAreBlockedWhileDummyRemainsUsableAndSwitchDoesNotDiscardIt()
    {
        const string lexware = "WawiCallPro.Plugins.Lexware.Pro";
        const string dummy = "WawiCallPro.Plugins.Dummy";
        using var fixture = new Fixture(lexware);
        var registry = new RegistryProbe();
        var log = new CaptureLog();
        var installation = new LexwareInstallation(registry, log);
        await using var runtime = new ProductPluginRuntime(fixture.Sessions, new(fixture.Root), installation: installation);
        await runtime.StartAsync(default);
        Assert.HasCount(1, log.Events);
        Assert.IsFalse(runtime.CanUseActivePlugin);
        Assert.IsFalse(runtime.CanActivate(lexware));
        Assert.IsTrue(runtime.CanActivate(dummy));
        var callbacks = 0;
        Task<int> Source() => runtime.UseAsync((_, _) => Task.FromResult(++callbacks));
        await Assert.ThrowsExactlyAsync<LexwareUnavailableException>(Source);
        await Assert.ThrowsExactlyAsync<LexwareUnavailableException>(() => new PluginBusinessDirectory(runtime).GetCompaniesAsync());
        Assert.AreEqual(0, callbacks);
        await runtime.SwitchAsync(dummy);
        Assert.AreEqual(1, await Source());
        var created = fixture.Sessions.Created;
        await Assert.ThrowsExactlyAsync<LexwareUnavailableException>(() => runtime.SwitchAsync(lexware));
        Assert.AreEqual(created, fixture.Sessions.Created);
        Assert.AreEqual(dummy, runtime.ActivePluginId);
        registry.FinOffice = true;
        runtime.GetAvailablePlugins();
        Assert.IsTrue(runtime.CanActivate(lexware), "Refreshing the plugin catalog must refresh availability after an installation.");
        await runtime.SwitchAsync(lexware);
        Assert.AreEqual(2, await Source());
        registry.FinOffice = false;
        await Assert.ThrowsExactlyAsync<LexwareUnavailableException>(Source);
        Assert.AreEqual(2, callbacks);
        Assert.AreEqual(lexware, runtime.ActivePluginId);
    }

    private sealed class RegistryProbe : ILexwareRegistryProbe
    {
        internal bool Pro, FinOffice;
        internal int Failure, ProChecks, FinChecks;
        public bool IsProInstalled() { ProChecks++; if ((Failure & 1) != 0) throw new IOException("synthetic-sensitive-value"); return Pro; }
        public bool IsFinOfficeInstalled() { FinChecks++; if ((Failure & 2) != 0) throw new IOException("synthetic-sensitive-value"); return FinOffice; }
    }
    private sealed class CaptureLog : ILogger<LexwareInstallation>
    {
        internal List<(LogLevel Level, OperationEvent Event)> Events = [];
        public bool IsEnabled(LogLevel level) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.IsNull(exception);
            Assert.IsTrue(OperationEvents.TryGetEvent(state, out var operation));
            Events.Add((level, operation));
        }
    }
    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wawi-installation-" + Guid.NewGuid().ToString("N"));
        internal Sessions Sessions { get; } = new();
        internal Fixture(string pluginId)
        {
            foreach (var id in new[] { pluginId, "WawiCallPro.Plugins.Dummy" })
            {
                var target = Path.Combine(Root, id); Directory.CreateDirectory(target);
                File.Copy(typeof(Fixture).Assembly.Location, Path.Combine(target, id + ".dll"));
            }
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
    private sealed class Sessions : IPluginSessionFactory
    {
        internal int Created;
        internal string? LastLoaded;
        public IPluginSession Create(IReadOnlySet<string> tokens) => CreateTrusted();
        public IPluginSession CreateTrusted() { Created++; return new Session(this); }
        private sealed class Session(Sessions owner) : IPluginSession, ISelectedPluginSession
        {
            public void Load(string path) => throw new NotSupportedException();
            public Task LoadAsync(string path) => LoadSelectedAsync(path);
            public Task LoadSelectedAsync(string path) { owner.LastLoaded = path; return Task.CompletedTask; }
            public IReadOnlyList<IWawiCallPlugin> GetPlugins() => [new Plugin()];
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
    private sealed class Plugin : IWawiCallPlugin
    {
        public string Name => "isolated-test";
        public Version Version => new(1, 0);
        public LexwareProductLine ProductLine => LexwareProductLine.Pro;
        public IBusinessDirectory Directory => throw new AssertFailedException("The guard must run before accessing a source.");
        public void Configure(IWawiCallHostServices services) { }
        public void Initialize(IPluginHost host) { }
        public void Execute() { }
        public void Shutdown() { }
    }
}
