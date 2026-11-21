using System.Runtime.CompilerServices;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class PluginSettingsRegistryTests
{
    private static readonly PluginSettingsTabDescriptor Lexware = new("lexware", "Lexware", 200);

    [TestMethod]
    public async Task RegistrationAndCatalogAreLazyAndActivateOnlyAfterConfiguration()
    {
        await using var registry = new PluginSettingsRegistry();
        await using var session = registry.CreateScope("Lexware.Pro");
        var factory = new UninvokedFactory();
        await using var registration = session.Ui.RegisterSettingsTab(Lexware, factory);
        Assert.IsEmpty(registry.GetSnapshot().Tabs);
        Assert.AreEqual(0L, registry.GetSnapshot().Revision);
        session.Activate();
        var snapshot = registry.GetSnapshot();
        var tab = snapshot.Tabs.Single();
        Assert.AreEqual("Lexware.Pro", tab.PluginId);
        Assert.AreEqual(session.Id, tab.Key.SessionId);
        Assert.AreEqual(Lexware, tab.Descriptor);
        using (var lease = registry.AcquireFactory(tab.Key)) Assert.AreSame(factory, lease.Factory);
        Assert.AreEqual(0, factory.Calls, "Neither registration, metadata reads nor acquisition may create a view.");
        session.Activate();
        Assert.AreEqual(snapshot.Revision, registry.GetSnapshot().Revision);
        Assert.AreNotEqual(typeof(PluginSettingsRegistry.Session), session.Ui.GetType());
    }

    [TestMethod]
    public async Task FailedPendingSessionLeavesNoVisibleOrAcquirableRegistration()
    {
        await using var registry = new PluginSettingsRegistry();
        var session = registry.CreateScope("Lexware.Pro");
        await using var registration = session.Ui.RegisterSettingsTab(Lexware, new UninvokedFactory());
        var key = new PluginSettingsTabKey(session.Id, Lexware.Id);
        AssertFailure(PluginSettingsRegistryFailure.UnknownTab, () => registry.AcquireFactory(key));
        await session.DisposeAsync();
        Assert.IsEmpty(registry.GetSnapshot().Tabs);
        AssertFailure(PluginSettingsRegistryFailure.SessionClosed, session.Activate);
        AssertFailure(PluginSettingsRegistryFailure.SessionClosed, () => session.Ui.RegisterSettingsTab(Lexware, new UninvokedFactory()));
    }

    [TestMethod]
    public async Task OnlyOneSessionCanBeActiveAndOldLeasesCannotRemoveItsReplacement()
    {
        await using var registry = new PluginSettingsRegistry();
        var first = registry.CreateScope("Lexware.Pro");
        await using var oldRegistration = first.Ui.RegisterSettingsTab(Lexware, new UninvokedFactory());
        first.Activate();
        var oldKey = registry.GetSnapshot().Tabs.Single().Key;
        await using var second = registry.CreateScope("Lexware.FinOffice");
        var replacement = new UninvokedFactory();
        await using var registration = second.Ui.RegisterSettingsTab(Lexware, replacement);
        AssertFailure(PluginSettingsRegistryFailure.ActiveSessionExists, second.Activate);
        Assert.AreEqual(oldKey, registry.GetSnapshot().Tabs.Single().Key);
        await first.DisposeAsync();
        second.Activate();
        await oldRegistration.DisposeAsync();
        AssertFailure(PluginSettingsRegistryFailure.UnknownTab, () => registry.AcquireFactory(oldKey));
        var newKey = registry.GetSnapshot().Tabs.Single().Key;
        Assert.AreNotEqual(oldKey.SessionId, newKey.SessionId);
        using var lease = registry.AcquireFactory(newKey);
        Assert.AreSame(replacement, lease.Factory);
    }

    [TestMethod]
    public async Task RevocationImmediatelyHidesTheTabAndWaitsForEveryFactoryLease()
    {
        await using var registry = new PluginSettingsRegistry();
        await using var session = registry.CreateScope("Lexware.Pro");
        var factory = new UninvokedFactory();
        var registration = session.Ui.RegisterSettingsTab(Lexware, factory);
        session.Activate();
        var snapshot = registry.GetSnapshot();
        var key = snapshot.Tabs.Single().Key;
        using var first = registry.AcquireFactory(key);
        using var second = registry.AcquireFactory(key);
        var revoke = registration.DisposeAsync().AsTask();
        Assert.IsFalse(revoke.IsCompleted);
        Assert.IsEmpty(registry.GetSnapshot().Tabs);
        Assert.HasCount(1, snapshot.Tabs, "An earlier detached metadata snapshot remains readable.");
        AssertFailure(PluginSettingsRegistryFailure.UnknownTab, () => registry.AcquireFactory(key));
        Assert.AreSame(factory, first.Factory);
        first.Dispose(); first.Dispose();
        Assert.IsFalse(revoke.IsCompleted);
        Assert.Throws<ObjectDisposedException>(() => _ = first.Factory);
        second.Dispose();
        await revoke.WaitAsync(TimeSpan.FromSeconds(5));
        await registration.DisposeAsync();
        Assert.AreEqual(snapshot.Revision + 1, registry.GetSnapshot().Revision);
    }

    [TestMethod]
    public async Task SessionCloseAlsoWaitsForAlreadyRevokedFactories()
    {
        await using var registry = new PluginSettingsRegistry();
        var session = registry.CreateScope("Lexware.Pro");
        var registration = session.Ui.RegisterSettingsTab(Lexware, new UninvokedFactory());
        session.Activate();
        using var lease = registry.AcquireFactory(registry.GetSnapshot().Tabs.Single().Key);
        var revoke = registration.DisposeAsync().AsTask();
        var close = session.DisposeAsync().AsTask();
        Assert.IsFalse(close.IsCompleted);
        lease.Dispose();
        await Task.WhenAll(revoke, close).WaitAsync(TimeSpan.FromSeconds(5));
        await session.DisposeAsync();
    }

    [TestMethod]
    public async Task RegistryShutdownClosesPendingAndActiveFacadesAndWaitsForAnAlreadyClosingSession()
    {
        var registry = new PluginSettingsRegistry();
        var active = registry.CreateScope("Lexware.Pro");
        var pending = registry.CreateScope("Lexware.FinOffice");
        await using var registration = active.Ui.RegisterSettingsTab(Lexware, new UninvokedFactory());
        await using var pendingRegistration = pending.Ui.RegisterSettingsTab(Lexware, new UninvokedFactory());
        active.Activate();
        using var lease = registry.AcquireFactory(registry.GetSnapshot().Tabs.Single().Key);
        var close = active.DisposeAsync().AsTask();
        var shutdown = registry.DisposeAsync().AsTask();
        Assert.IsFalse(shutdown.IsCompleted);
        Assert.IsEmpty(registry.GetSnapshot().Tabs);
        AssertFailure(PluginSettingsRegistryFailure.SessionClosed, pending.Activate);
        Assert.Throws<ObjectDisposedException>(() => registry.CreateScope("Other"));
        Assert.Throws<ObjectDisposedException>(() => registry.AcquireFactory(new(active.Id, "lexware")));
        lease.Dispose();
        await Task.WhenAll(close, shutdown).WaitAsync(TimeSpan.FromSeconds(5));
        await registry.DisposeAsync();
    }

    [TestMethod]
    public async Task PendingRegistrationCanBeReplacedWithoutOldLeaseRevokingTheNewOne()
    {
        await using var registry = new PluginSettingsRegistry();
        await using var session = registry.CreateScope("Lexware.Pro");
        var first = session.Ui.RegisterSettingsTab(Lexware, new UninvokedFactory());
        await first.DisposeAsync();
        var factory = new UninvokedFactory();
        await using var second = session.Ui.RegisterSettingsTab(Lexware, factory);
        await first.DisposeAsync();
        session.Activate();
        using var lease = registry.AcquireFactory(registry.GetSnapshot().Tabs.Single().Key);
        Assert.AreSame(factory, lease.Factory);
        AssertFailure(PluginSettingsRegistryFailure.RegistrationClosed,
            () => session.Ui.RegisterSettingsTab(new("late", "Late", 300), factory));
    }

    [TestMethod]
    public async Task DuplicateRegistrationsAreAtomicAndDoNotReplaceTheWinner()
    {
        await using var registry = new PluginSettingsRegistry();
        await using var session = registry.CreateScope("Lexware.Pro");
        var factories = Enumerable.Range(0, 16).Select(_ => new UninvokedFactory()).ToArray();
        var accepted = await Task.WhenAll(factories.Select(factory => Task.Run(() =>
        {
            try { session.Ui.RegisterSettingsTab(Lexware, factory); return factory; }
            catch (PluginSettingsRegistryException failure) when (failure.Code == PluginSettingsRegistryFailure.DuplicateTab) { return null; }
        })));
        var winner = accepted.Single(factory => factory is not null);
        session.Activate();
        using var lease = registry.AcquireFactory(registry.GetSnapshot().Tabs.Single().Key);
        Assert.AreSame(winner, lease.Factory);
        Assert.IsTrue(factories.All(factory => factory.Calls == 0));
    }

    [TestMethod]
    public async Task CatalogIsDetachedOrderedAndAcceptsUnicodeTitles()
    {
        await using var registry = new PluginSettingsRegistry();
        await using var session = registry.CreateScope("Lexware.Pro");
        foreach (var descriptor in new[] { new PluginSettingsTabDescriptor("b", "日本語 🧩", 200), new("z", "Deutsch", 10), new("a", "Français", 200) })
            session.Ui.RegisterSettingsTab(descriptor, new UninvokedFactory());
        session.Activate();
        CollectionAssert.AreEqual(new[] { "z", "a", "b" }, registry.GetSnapshot().Tabs.Select(tab => tab.Descriptor.Id).ToArray());
    }

    [TestMethod]
    [DataRow("", "Lexware", 200)]
    [DataRow("Lexware", "Lexware", 200)]
    [DataRow("a/b", "Lexware", 200)]
    [DataRow("a b", "Lexware", 200)]
    [DataRow("1abc", "Lexware", 200)]
    [DataRow("lexware", "", 200)]
    [DataRow("lexware", " leading", 200)]
    [DataRow("lexware", "trailing ", 200)]
    [DataRow("lexware", "line\nbreak", 200)]
    [DataRow("lexware", "hidden\u202etext", 200)]
    [DataRow("lexware", "Lexware", -1)]
    [DataRow("lexware", "Lexware", 10001)]
    public async Task InvalidMetadataIsRejectedWithoutCallingFactoryOrEchoingValues(string id, string title, int order)
    {
        await using var registry = new PluginSettingsRegistry();
        await using var session = registry.CreateScope("Lexware.Pro");
        var factory = new UninvokedFactory();
        var failure = AssertFailure(PluginSettingsRegistryFailure.InvalidDescriptor,
            () => session.Ui.RegisterSettingsTab(new(id, title, order), factory));
        Assert.AreEqual("Plugin-Einstellungsregistrierung: InvalidDescriptor", failure.Message);
        Assert.IsEmpty(registry.GetSnapshot().Tabs);
        Assert.AreEqual(0, factory.Calls);
    }

    [TestMethod]
    public async Task MetadataAndScopeLimitsFailBeforeRetainingAnotherFactory()
    {
        await using var registry = new PluginSettingsRegistry();
        await using var session = registry.CreateScope("Lexware.Pro");
        var factory = new UninvokedFactory();
        AssertFailure(PluginSettingsRegistryFailure.InvalidDescriptor, () => session.Ui.RegisterSettingsTab(new(new string('a', 65), "Lexware", 1), factory));
        AssertFailure(PluginSettingsRegistryFailure.InvalidDescriptor, () => session.Ui.RegisterSettingsTab(new("a", new string('x', 121), 1), factory));
        AssertFailure(PluginSettingsRegistryFailure.InvalidDescriptor, () => session.Ui.RegisterSettingsTab(new("a", "invalid\ud800", 1), factory));
        Assert.Throws<ArgumentNullException>(() => session.Ui.RegisterSettingsTab(null!, factory));
        Assert.Throws<ArgumentNullException>(() => session.Ui.RegisterSettingsTab(Lexware, null!));
        for (var index = 0; index < 32; index++) session.Ui.RegisterSettingsTab(new("tab-" + index, "Tab", index), factory);
        AssertFailure(PluginSettingsRegistryFailure.LimitExceeded, () => session.Ui.RegisterSettingsTab(Lexware, factory));
        for (var index = 1; index < 32; index++) registry.CreateScope("Pending." + index);
        AssertFailure(PluginSettingsRegistryFailure.LimitExceeded, () => registry.CreateScope("Overflow"));
    }

    [TestMethod]
    public async Task FactoryPinsAreBoundedAndRevocationDrainsConcurrentReleases()
    {
        await using var registry = new PluginSettingsRegistry();
        await using var session = registry.CreateScope("Lexware.Pro");
        var registration = session.Ui.RegisterSettingsTab(Lexware, new UninvokedFactory());
        session.Activate();
        var key = registry.GetSnapshot().Tabs.Single().Key;
        var leases = Enumerable.Range(0, 1024).Select(_ => registry.AcquireFactory(key)).ToArray();
        AssertFailure(PluginSettingsRegistryFailure.LimitExceeded, () => registry.AcquireFactory(key));
        var closing = registration.DisposeAsync().AsTask();
        await Task.WhenAll(leases.Select(lease => Task.Run(() => { lease.Dispose(); lease.Dispose(); })));
        await closing.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task RevocationDropsFactoryEvenWhenMetadataAndRegistrationLeasesAreRetained()
    {
        await using var registry = new PluginSettingsRegistry();
        await using var session = registry.CreateScope("Lexware.Pro");
        var (registration, weak) = RegisterWeakFactory(session);
        session.Activate();
        var snapshot = registry.GetSnapshot();
        await registration.DisposeAsync();
        for (var count = 0; count < 3 && weak.IsAlive; count++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
        Assert.IsFalse(weak.IsAlive, "Detached metadata and revoked leases must not retain plugin code.");
        GC.KeepAlive(snapshot); GC.KeepAlive(registration);
    }

    [TestMethod]
    public void OptionalUiContractDoesNotContaminateBaseHostOrExposeTheRegistryToPlugins()
    {
        Assert.IsTrue(typeof(IWawiCallHostServices).IsAssignableFrom(typeof(IWindowsPluginHostServices)));
        Assert.IsFalse(typeof(IWawiCallHostServices).GetProperties().Any(property => property.PropertyType == typeof(IPluginHostUi)));
        Assert.HasCount(1, typeof(IPluginHostUi).GetMethods());
        Assert.IsTrue(typeof(IPluginLifetimeResource).IsAssignableFrom(typeof(IPluginSettingsRegistration)));
        Assert.AreSame(typeof(IPluginViewHandle).Assembly, typeof(IPluginSettingsPage).Assembly);
        Assert.IsTrue(typeof(IPluginSettingsPage).Assembly.GetCustomAttributes(false).Any(attribute => attribute.GetType().FullName == "Quantarix.Plugin.Abstractions.SharedAssemblyAttribute"));
        Assert.IsTrue(typeof(RegisteredPluginSettingsTab).GetProperties().All(property => !typeof(IPluginSettingsPageFactory).IsAssignableFrom(property.PropertyType)));
    }

    private static PluginSettingsRegistryException AssertFailure(PluginSettingsRegistryFailure code, Action action)
    {
        var failure = Assert.Throws<PluginSettingsRegistryException>(action);
        Assert.AreEqual(code, failure.Code);
        return failure;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IPluginSettingsRegistration Registration, WeakReference Factory) RegisterWeakFactory(PluginSettingsRegistry.Session session)
    {
        var factory = new UninvokedFactory();
        return (session.Ui.RegisterSettingsTab(Lexware, factory), new(factory));
    }

    private sealed class UninvokedFactory : IPluginSettingsPageFactory
    {
        internal int Calls;
        public IPluginSettingsPage Create(IPluginSettingsPageContext context)
        { Calls++; throw new InvalidOperationException("A registration-only test must not construct UI."); }
    }
}
