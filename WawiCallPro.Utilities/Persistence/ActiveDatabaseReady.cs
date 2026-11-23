using System.Threading.Channels;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Coalesces notifications from the database wizard when the active target is ready.</summary>
public sealed class ActiveDatabaseReady
{
    private readonly Channel<bool> _notifications = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    internal void Notify() => _notifications.Writer.TryWrite(true);
    internal IAsyncEnumerable<bool> ReadAllAsync(CancellationToken token) => _notifications.Reader.ReadAllAsync(token);
}
