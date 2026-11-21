using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugin.Windows;

public interface IPluginViewHandle : IPluginLifetimeResource
{
    Control View { get; }
    bool HasUnsavedChanges => false;
}
public interface IPluginViewAccess
{
    Task<IPluginViewHandle> OpenAsync(string viewId, PartyKey party, CancellationToken cancellationToken = default);
    Task<IPluginSettingsHandle?> OpenSettingsAsync(WawiCallPro.Interfaces.Plugins.IPluginConnectionEditContext context,
        CancellationToken cancellationToken = default) => Task.FromResult<IPluginSettingsHandle?>(null);
}
/// <summary>Creation and lease disposal run on the host UI dispatcher. Runtime owns outstanding handles until stop.</summary>
public sealed class PluginViewAccess(IProductPluginRuntime runtime, IUiDispatcher dispatcher) : IPluginViewAccess
{
    public async Task<IPluginSettingsHandle?> OpenSettingsAsync(IPluginConnectionEditContext context, CancellationToken cancellationToken = default)
    {
        SettingsHandle? created = null;
        try
        {
            return await runtime.UseAsync<IPluginSettingsHandle?>(async (plugin, token) =>
            {
                if (plugin is not IWawiCallPluginSettings { HasConnectionSettings: true } settings) return null;
                var title = settings.ConnectionSettingsTitle;
                if (string.IsNullOrWhiteSpace(title) || title.Length > 120 || title.Any(char.IsControl))
                    throw new InvalidOperationException("Invalid plugin settings title.");
                await dispatcher.InvokeAsync(() => created = new SettingsHandle(settings.CreateConnectionSettings(context), title, dispatcher), token).ConfigureAwait(false);
                return created;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch { if (created is not null) await created.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private sealed class SettingsHandle(IPluginSettingsView view, string title, IUiDispatcher dispatcher) : IPluginSettingsHandle
    {
        private IPluginSettingsView? _view = view;
        private readonly SemaphoreSlim _gate = new(1);
        public string Title { get; } = title;
        public Control View => _view?.View ?? throw new ObjectDisposedException(nameof(SettingsHandle));
        public bool HasUnsavedChanges => _view?.HasUnsavedChanges == true;
        public void Reload(IPluginConnectionEditContext context) => (_view ?? throw new ObjectDisposedException(nameof(SettingsHandle))).Reload(context);
        public void ApplyChanges(IPluginConnectionEditContext context) => (_view ?? throw new ObjectDisposedException(nameof(SettingsHandle))).ApplyChanges(context);
        public async ValueTask DisposeAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_view is not { } current) return;
                await dispatcher.InvokeAsync(current.Dispose).ConfigureAwait(false);
                _view = null;
            }
            finally { _gate.Release(); }
        }
    }

    public async Task<IPluginViewHandle> OpenAsync(string viewId, PartyKey party, CancellationToken cancellationToken = default)
    {
        IPluginViewHandle? created = null;
        try { return await runtime.UseAsync<IPluginViewHandle>(async (plugin, token) =>
        {
            if (plugin is not IWawiCallPluginViews views) throw new NotSupportedException("Dieses Plugin stellt keine Windows-Views bereit.");
            IPluginViewLease? lease = null;
            if (plugin is IAsyncWawiCallPluginViews asynchronous)
            {
                Task<IPluginViewLease>? loading = null;
                await dispatcher.InvokeAsync(() => loading = asynchronous.CreateViewAsync(viewId, party, token), token).ConfigureAwait(false);
                lease = await (loading ?? throw new InvalidOperationException("Plugin did not start a view request.")).ConfigureAwait(false);
            }
            else await dispatcher.InvokeAsync(() => lease = views.CreateView(viewId, party), token).ConfigureAwait(false);
            return created = new Handle(lease ?? throw new InvalidOperationException("Plugin did not create a view."), dispatcher);
        }, cancellationToken).ConfigureAwait(false); }
        catch { if (created is not null) await created.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private sealed class Handle(IPluginViewLease lease, IUiDispatcher dispatcher) : IPluginViewHandle
    {
        private IPluginViewLease? _lease = lease;
        private readonly SemaphoreSlim _gate = new(1);
        public Control View => _lease?.View ?? throw new ObjectDisposedException(nameof(Handle));
        public bool HasUnsavedChanges => _lease is IPluginViewEditState edits && edits.HasUnsavedChanges;
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
