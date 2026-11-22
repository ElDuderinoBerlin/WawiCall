using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugins.Lexware.Pro;

public interface IProDirectoryLease : IPluginConnectionDirectory
{
}

/// <summary>Owns a separate transport/token session for a committed account or an explicit editor preview.</summary>
public interface IProDirectoryFactory : IPluginConnectionDirectoryFactory
{
    new IProDirectoryLease Create(ProConnectionConfiguration settings);
}

public sealed class ProDirectoryFactory(IProRegistry registry, IProHttpHandlerFactory handlers, IProPropertyStore names, TimeProvider clock) : IProDirectoryFactory
{
    IPluginConnectionDirectory IPluginConnectionDirectoryFactory.Create(ProConnectionConfiguration settings) => Create(settings);
    public IProDirectoryLease Create(ProConnectionConfiguration settings)
    {
        var options = new ProConnectionOptions
        {
            ApiUrl = settings.ApiUrl, InstallationId = settings.InstallationId, Username = settings.Username,
            Password = settings.Password, ClientKey = settings.ClientKey,
            TimeoutSeconds = settings.TimeoutSeconds, TokenTimeoutSeconds = settings.TokenTimeoutSeconds
        };
        var session = new ProApiSession(new ProInstallationSource(registry, options), new ProApiConnectionFactory(options, handlers), clock);
        return new Lease(new ProBusinessDirectory(session, names, new ProPartyDirectory(session)), session);
    }
    private sealed class Lease(IBusinessDirectory directory, IProApiSession session) : IProDirectoryLease
    {
        public IBusinessDirectory Directory => directory;
        public ValueTask DisposeAsync() => session.DisposeAsync();
    }
}
