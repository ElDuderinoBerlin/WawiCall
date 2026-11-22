using System.Collections.Immutable;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugins.Lexware.Pro.Tests;

[TestClass]
public sealed class ReloadingDirectoryTests
{
    [TestMethod]
    public async Task CommittedAccountChangesReplaceTheSessionAndRemovalFailsClosed()
    {
        var configuration = new Configuration(); var factory = new Factory();
        await using var directory = new ReloadingProBusinessDirectory(configuration, factory, new(null!, null!, null!));
        Assert.IsFalse(directory.IsConnectionConfigured);
        await Assert.ThrowsExactlyAsync<ProAdapterException>(() => directory.GetCompaniesAsync());
        Assert.AreEqual(0, factory.Created.Count);
        configuration.Set("A");
        Assert.AreEqual("A", (await directory.GetCompaniesAsync())[0].Key.InstallationId);
        await directory.GetCompaniesAsync(); Assert.AreEqual(1, factory.Created.Count);
        configuration.Set("B");
        Assert.AreEqual("B", (await directory.GetCompaniesAsync())[0].Key.InstallationId);
        Assert.IsTrue(factory.Created[0].Disposed);
        Assert.IsFalse(factory.Created[1].Disposed);
        configuration.Set(null);
        await Assert.ThrowsExactlyAsync<ProAdapterException>(() => directory.GetCompaniesAsync());
        Assert.IsTrue(factory.Created[1].Disposed);
    }
    [TestMethod]
    public async Task OldInFlightAccountCannotPublishAfterSaveAndShutdownWaitsForItsOwner()
    {
        var configuration = new Configuration(); configuration.Set("A"); var factory = new Factory();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Wait = async token => { entered.TrySetResult(); await release.Task.WaitAsync(token); };
        await using var directory = new ReloadingProBusinessDirectory(configuration, factory, new(null!, null!, null!));
        var reading = directory.GetCompaniesAsync(); await entered.Task;
        configuration.Set("B");
        var next = directory.GetCompaniesAsync(); Assert.IsFalse(next.IsCompleted);
        Assert.IsFalse(factory.Created[0].Disposed);
        release.TrySetResult();
        var rejected = await Assert.ThrowsExactlyAsync<ProAdapterException>(() => reading);
        Assert.AreEqual(ProFailure.ForeignCompany, rejected.Failure);
        Assert.AreEqual("B", (await next)[0].Key.InstallationId);
        Assert.IsTrue(factory.Created[0].Disposed);
        await directory.DisposeAsync(); Assert.IsTrue(factory.Created[1].Disposed);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => directory.GetCompaniesAsync());
    }
    private sealed class Configuration : IPluginConnectionConfiguration
    {
        public long Revision { get; private set; }
        private ProConnectionConfiguration? _current;
        internal void Set(string? installation) { _current = installation is null ? null : new() { InstallationId = installation }; Revision++; }
        public ProConnectionConfiguration? ReadPro() => _current;
        public string? ReadFinOfficeInstallationId() => null;
    }
    private sealed class Factory : IProDirectoryFactory
    {
        internal List<Lease> Created = [];
        internal Func<CancellationToken, Task>? Wait;
        public IProDirectoryLease Create(ProConnectionConfiguration settings)
        {
            var lease = new Lease(settings.InstallationId!, Wait); Created.Add(lease); return lease;
        }
        IPluginConnectionDirectory IPluginConnectionDirectoryFactory.Create(ProConnectionConfiguration settings) => Create(settings);
    }
    private sealed class Lease(string installation, Func<CancellationToken, Task>? wait) : IProDirectoryLease, IBusinessDirectory
    {
        internal bool Disposed;
        public IBusinessDirectory Directory => this;
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
        public async Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default)
        {
            if (wait is not null) await wait(cancellationToken);
            return [new(new(LexwareProductLine.Pro, installation, "1"), "Testfirma")];
        }
        public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
