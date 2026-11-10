using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Core.PhoneNumbers;

public sealed class CallViewScopeFactory : ICallViewScopeFactory
{
    public ICallViewScope Create(CallSnapshot call) => new CallViewScope(call);
}
internal sealed class CallViewScope : ICallViewScope
{
    private readonly object _gate = new();
    private CancellationTokenSource _selection = new();
    private CallSnapshot _call;
    private CallViewContext? _current;
    private long _revision;
    private bool _disposed;
    private Task? _disposeTask;
    internal CallViewScope(CallSnapshot call)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (call.Id == Guid.Empty || call.Revision < 0) throw new ArgumentException("Invalid call identity.", nameof(call));
        _call = call;
    }
    public Guid CallId => _call.Id;
    public CallSnapshot Call { get { lock (_gate) return _call; } }
    public CallViewContext? Current { get { lock (_gate) return _current; } }
    public bool IsEnded { get { lock (_gate) return _disposed || Ended(_call); } }
    public async Task<bool> ObserveAsync(CallSnapshot call)
    {
        CancellationTokenSource? old = null;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (call.Id != CallId) throw new ArgumentException("Wrong call identity.", nameof(call));
            if (call.Revision <= _call.Revision || Ended(_call)) return false;
            _call = call;
            if (Ended(call)) { old = _selection; _selection = new(); _revision++; }
        }
        if (old is not null) await CancelAsync(old).ConfigureAwait(false);
        return true;
    }
    public async Task<CallViewContext?> SelectAsync(CallerMatchDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        CancellationTokenSource old;
        CallViewContext? context = null;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Ended(_call)) throw new InvalidOperationException("Call has ended.");
            if (decision.Selection is { } selected)
            {
                var group = decision.Groups.Single(group => group.Company.Company == selected.Party.Company);
                var candidate = group.Candidates.Single(candidate => candidate.Party == selected.Party);
                context = new(CallId, checked(_revision + 1), selected.Party, group.Company.DisplayName, candidate.DisplayName);
            }
            _revision = checked(_revision + 1); _current = context;
            old = _selection; _selection = new();
        }
        await CancelAsync(old).ConfigureAwait(false);
        return context;
    }
    public ICallViewOperation BeginOperation(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_current is null || Ended(_call)) throw new InvalidOperationException("No active call selection.");
            return new Operation(this, _current, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _selection.Token));
        }
    }
    private bool Publish(CallViewContext context, CancellationToken token, Action publish)
    {
        lock (_gate)
        {
            if (_disposed || Ended(_call) || _current != context || context.Revision != _revision || token.IsCancellationRequested) return false;
            publish(); return true;
        }
    }
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposeTask is not null) return new(_disposeTask);
            _disposed = true; _revision++;
            return new(_disposeTask = CancelAsync(_selection));
        }
    }
    private static bool Ended(CallSnapshot call) => call.State is CallState.Ended or CallState.Failed;
    private static async Task CancelAsync(CancellationTokenSource source)
    { try { await source.CancelAsync().ConfigureAwait(false); } finally { source.Dispose(); } }
    private sealed class Operation(CallViewScope scope, CallViewContext context, CancellationTokenSource cancellation) : ICallViewOperation
    {
        private int _disposed;
        public CallViewContext Context { get; } = context;
        public CancellationToken CancellationToken { get; } = cancellation.Token;
        public async ValueTask<bool> PublishAsync(IUiDispatcher dispatcher, Action publish)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            var accepted = false;
            await dispatcher.InvokeAsync(() => accepted = scope.Publish(Context, CancellationToken, publish), CancellationToken).ConfigureAwait(false);
            return accepted;
        }
        public void Dispose()
        { if (Interlocked.Exchange(ref _disposed, 1) == 0) { cancellation.Cancel(); cancellation.Dispose(); } }
    }
}
