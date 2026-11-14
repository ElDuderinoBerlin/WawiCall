using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class PluginPartySearchTests
{
    private static readonly CompanyKey Company = new(LexwareProductLine.Pro, "fixture", "1");
    private static Party Row(string id) => new() { Key = new(Company, PartyKind.Customer, id), DisplayName = "Beispiel " + id };

    [TestMethod]
    public async Task HostRegistrationUsesOneRuntimeOperationForAllPages()
    {
        var source = new Source((request, _) => Task.FromResult(new PartyPage(Company,
            request.Page.Offset == 0 ? new([Row("1")], true) : new([Row("2")], false))));
        var runtime = new Runtime(new Plugin(source));
        var services = new ServiceCollection();
        services.AddProductPluginRuntime(); services.AddSingleton<IProductPluginRuntime>(runtime);
        using var provider = services.BuildServiceProvider();
        var search = provider.GetRequiredService<IPartySearch>();
        Assert.AreSame(search, provider.GetRequiredService<IPartySearch>());
        var result = await search.SearchAsync(new(Company, "Beispiel"));
        Assert.HasCount(2, result.Items); Assert.AreEqual(2, source.Calls); Assert.AreEqual(1, runtime.Calls);
    }

    [TestMethod]
    public async Task RealRuntimeShutdownCancelsAndAwaitsTheCompleteSearchIncludingNonCooperativeSource()
    {
        var secondPage = new TaskCompletionSource<PartyPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new Source(async (request, token) =>
        {
            if (request.Page.Offset == 0) return new(Company, new([Row("1")], true));
            using var registration = token.Register(() => cancelled.TrySetResult());
            entered.SetResult();
            return await secondPage.Task;
        });
        var session = new Session(new Plugin(source));
        var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-search-" + Guid.NewGuid().ToString("N"));
        await using var runtime = new ProductPluginRuntime(new Sessions(session), new(root));
        await runtime.StartAsync(default);
        var search = new PluginPartySearch(runtime, new());
        var pending = search.SearchAsync(new(Company, "Beispiel"));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var stop = runtime.StopAsync(default);
        try
        {
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.IsFalse(stop.IsCompleted); Assert.IsFalse(session.Disposed);
        }
        finally { secondPage.TrySetResult(new(Company, new([Row("2")], false))); }
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        await stop.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsTrue(session.Disposed); Assert.AreEqual(2, source.Calls);
        Assert.IsFalse(Directory.Exists(root), "The isolated fake session must not create files.");
    }

    private sealed class Runtime(IWawiCallPlugin plugin) : IProductPluginRuntime
    {
        internal int Calls;
        public bool IsLoaded => true;
        public Task<T> UseAsync<T>(Func<IWawiCallPlugin, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        { Calls++; return operation(plugin, cancellationToken); }
    }
    private sealed class Plugin(IBusinessDirectory source) : IWawiCallPlugin
    {
        public string Name => "Isolated search source";
        public Version Version => new(1, 0);
        public LexwareProductLine ProductLine => LexwareProductLine.Pro;
        public IBusinessDirectory Directory => source;
        public void Configure(IWawiCallHostServices services) { }
        public void Initialize(IPluginHost host) { }
        public void Execute() => throw new NotSupportedException();
        public void Shutdown() { }
    }
    private sealed class Sessions(Session session) : IPluginSessionFactory
    {
        public IPluginSession Create(IReadOnlySet<string> tokens) => session;
        public IPluginSession CreateTrusted() => session;
    }
    private sealed class Session(IWawiCallPlugin plugin) : IPluginSession
    {
        internal bool Disposed;
        public IReadOnlyList<IWawiCallPlugin> GetPlugins() => [plugin];
        public void Load(string path) { }
        public Task LoadAsync(string path) => Task.CompletedTask;
        public void Dispose() => Disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
    private sealed class Source(Func<PartyQuery, CancellationToken, Task<PartyPage>> read) : IBusinessDirectory
    {
        internal int Calls;
        public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default)
        { Calls++; return read(query, cancellationToken); }
        public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default) => throw new AssertFailedException();
        public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default) => throw new AssertFailedException();
        public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default) => throw new AssertFailedException();
    }
}
