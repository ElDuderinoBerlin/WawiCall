namespace WawiCallPro.ComPrototype;

/// <summary>Explicit host ownership, independent of the COM library's private message loop.</summary>
internal sealed class ServerLifetime(TimeProvider clock, TimeSpan idleDelay)
{
    private readonly object _gate = new();
    private DateTimeOffset _changed = clock.GetUtcNow();
    private int _clients;
    private int _locks;
    private bool _initializing = true;
    private bool _userOwned;
    private bool _callActive;
    private bool _stopping;

    internal IDisposable AcquireClient()
    {
        lock (_gate)
        {
            if (_stopping) throw new InvalidOperationException("Server stopping.");
            _clients++;
            _changed = clock.GetUtcNow();
            return new Lease(this);
        }
    }

    internal void LockServer(bool acquire)
    {
        lock (_gate)
        {
            if (_stopping || (!acquire && _locks == 0)) throw new InvalidOperationException("Invalid server lock.");
            _locks += acquire ? 1 : -1;
            _changed = clock.GetUtcNow();
        }
    }

    internal void CompleteStartup() { lock (_gate) { _initializing = false; _changed = clock.GetUtcNow(); } }
    internal void SetUserOwned(bool value) { lock (_gate) { _userOwned = value; _changed = clock.GetUtcNow(); } }
    internal void SetCallActive(bool value) { lock (_gate) { _callActive = value; _changed = clock.GetUtcNow(); } }
    internal int Clients { get { lock (_gate) return _clients; } }

    internal bool TryBeginShutdown()
    {
        lock (_gate)
        {
            if (_stopping || _initializing || _clients != 0 || _locks != 0 || _userOwned || _callActive
                || clock.GetUtcNow() - _changed < idleDelay) return false;
            _stopping = true;
            return true;
        }
    }

    private void ReleaseClient()
    {
        lock (_gate) { _clients--; _changed = clock.GetUtcNow(); }
    }

    private sealed class Lease(ServerLifetime owner) : IDisposable
    {
        private ServerLifetime? _owner = owner;
        public void Dispose() { Interlocked.Exchange(ref _owner, null)?.ReleaseClient(); GC.SuppressFinalize(this); }
        ~Lease() { Interlocked.Exchange(ref _owner, null)?.ReleaseClient(); }
    }
}
