using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugin.Windows;

public interface IPluginViewHandle : IPluginLifetimeResource
{
    Control View { get; }
}
public interface IPluginViewAccess
{
    Task<IPluginViewHandle> OpenAsync(string viewId, PartyKey party, CancellationToken cancellationToken = default);
}
/// <summary>Creation and lease disposal run on the host UI dispatcher. Runtime owns outstanding handles until stop.</summary>
public sealed class PluginViewAccess(IProductPluginRuntime runtime, IUiDispatcher dispatcher) : IPluginViewAccess
{
    public Task<IPluginViewHandle> OpenAsync(string viewId, PartyKey party, CancellationToken cancellationToken = default) =>
        runtime.UseAsync<IPluginViewHandle>(async (plugin, token) =>
        {
            if (plugin is not IWawiCallPluginViews views) throw new NotSupportedException("Dieses Plugin stellt keine Windows-Views bereit.");
            IPluginViewLease? lease = null;
            await dispatcher.InvokeAsync(() => lease = views.CreateView(viewId, party), token).ConfigureAwait(false);
            return new Handle(lease ?? throw new InvalidOperationException("Plugin did not create a view."), dispatcher);
        }, cancellationToken);

    private sealed class Handle(IPluginViewLease lease, IUiDispatcher dispatcher) : IPluginViewHandle
    {
        private IPluginViewLease? _lease = lease;
        private readonly SemaphoreSlim _gate = new(1);
        public Control View => _lease?.View ?? throw new ObjectDisposedException(nameof(Handle));
        public async ValueTask DisposeAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_lease is not { } current) return;
                await dispatcher.InvokeAsync(current.Dispose).ConfigureAwait(false);
                _lease = null;
            }
            finally { _gate.Release(); }
        }
    }
}
