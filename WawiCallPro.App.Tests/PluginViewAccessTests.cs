using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class PluginViewAccessTests
{
    [TestMethod]
    public async Task ViewCreationAndDisposalAreDispatchedToTheOwningUiThread()
    {
        var completed = new TaskCompletionSource<(int Owner, int Created, int Disposed, bool IsDisposed)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(context);
            var owner = Environment.CurrentManagedThreadId;
            var plugin = new ViewPlugin();
            var access = new PluginViewAccess(new TestRuntime(plugin), new SynchronizationContextUiDispatcher(context));
            EventHandler? start = null;
            start = async (_, _) =>
            {
                Application.Idle -= start;
                try
                {
                    var key = new PartyKey(new(LexwareProductLine.Pro, "test", "1"), PartyKind.Customer, "1");
                    var handle = await Task.Run(() => access.OpenAsync("test", key));
                    var control = handle.View;
                    await Task.Run(async () => await handle.DisposeAsync());
                    await handle.DisposeAsync();
                    completed.SetResult((owner, plugin.CreatedOn, plugin.DisposedOn, control.IsDisposed));
                }
                catch (Exception exception) { completed.SetException(exception); }
                finally { Application.ExitThread(); }
            };
            Application.Idle += start;
            Application.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.AreEqual(result.Owner, result.Created);
        Assert.AreEqual(result.Owner, result.Disposed);
        Assert.IsTrue(result.IsDisposed);
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2)));
    }
    private sealed class TestRuntime(IWawiCallPlugin plugin) : IProductPluginRuntime
    {
        public bool IsLoaded => true;
        public Task<T> UseAsync<T>(Func<IWawiCallPlugin, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => operation(plugin, cancellationToken);
    }
    private sealed class ViewPlugin : IWawiCallPlugin, IWawiCallPluginViews
    {
        public int CreatedOn { get; private set; }
        public int DisposedOn { get; private set; }
        public string Name => "View fixture";
        public Version Version { get; } = new(1, 0);
        public LexwareProductLine ProductLine => LexwareProductLine.Pro;
        public IBusinessDirectory Directory => throw new NotSupportedException();
        public void Initialize(Quantarix.Plugin.Abstractions.IPluginHost host) { }
        public void Configure(IWawiCallHostServices services) { }
        public void Execute() => throw new NotSupportedException();
        public void Shutdown() { }
        public IReadOnlyList<PluginViewDescriptor> GetViews() => [new("test", "Test")];
        public IPluginViewLease CreateView(string id, PartyKey party)
        {
            CreatedOn = Environment.CurrentManagedThreadId;
            return new Lease(new Panel(), () => DisposedOn = Environment.CurrentManagedThreadId);
        }
        private sealed class Lease(Control view, Action disposed) : IPluginViewLease
        {
            public Control View => view;
            public void Dispose() { view.Dispose(); disposed(); }
        }
    }
}
