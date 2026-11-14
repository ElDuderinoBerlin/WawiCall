using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugin.Windows;

/// <summary>Host-owned appearance and isolated layout storage, borrowed by plugin views.</summary>
public interface IPartySearchPresentation
{
    void SizeButtons(params SimpleButton[] buttons);
    void Restore(GridView view);
    void Save(GridView view);
}
public interface IPartySearchWorkspaceAccess
{
    Task<IPluginViewHandle> OpenAsync(CancellationToken cancellationToken = default);
}

/// <summary>Creates only static UI; source operations start after the runtime creation callback has returned.</summary>
public sealed class PartySearchWorkspaceFactory(IBusinessDirectory directory, IPartySearch search, IPartySearchPresentation presentation,
    IUiDispatcher dispatcher) : IPartySearchWorkspaceFactory
{
    public IPluginLifetimeResource Create() => new Handle(new PartySearchPage(directory, search, presentation), dispatcher);
    private sealed class Handle(PartySearchPage page, IUiDispatcher dispatcher) : IPluginViewHandle
    {
        private PartySearchPage? _page = page;
        private readonly SemaphoreSlim _gate = new(1);
        public Control View => _page ?? throw new ObjectDisposedException(nameof(Handle));
        public async ValueTask DisposeAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_page is not { } current) return;
                Task? stopping = null;
                await dispatcher.InvokeAsync(() => stopping = current.StopAsync()).ConfigureAwait(false);
                await stopping!.ConfigureAwait(false);
                await dispatcher.InvokeAsync(current.Dispose).ConfigureAwait(false);
                _page = null;
            }
            finally { _gate.Release(); }
        }
    }
}

/// <summary>The plugin opts into and owns its search workspace through the existing runtime resource gate.</summary>
public sealed class PartySearchWorkspaceAccess(IProductPluginRuntime runtime, IPartySearchWorkspaceFactory factory,
    IUiDispatcher dispatcher) : IPartySearchWorkspaceAccess
{
    public async Task<IPluginViewHandle> OpenAsync(CancellationToken cancellationToken = default)
    {
        IPluginViewHandle? created = null;
        try
        {
            return await runtime.UseAsync(async (plugin, token) =>
            {
                if (plugin is not IWawiCallPluginWorkspaces workspaces) throw new NotSupportedException("Das Plugin bietet keine Geschäftspartnersuche an.");
                await dispatcher.InvokeAsync(() => created = workspaces.CreatePartySearch(factory) as IPluginViewHandle
                    ?? throw new InvalidOperationException("Invalid plugin workspace."), token).ConfigureAwait(false);
                return created!;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch { if (created is not null) await created.DisposeAsync().ConfigureAwait(false); throw; }
    }
}
