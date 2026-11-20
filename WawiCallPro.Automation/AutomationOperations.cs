using System.Threading.Channels;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Automation;

internal sealed record AutomationCommand(string Kind, CompanyKey? Company = null, string Value = "", string Context = "", Guid? RequestId = null);

/// <summary>One bounded worker per host. In-memory deduplication does not imply exactly-once execution after a crash.</summary>
public sealed class AutomationOperations : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Entry> _entries = [];
    private readonly Dictionary<Guid, Guid> _requests = [];
    private readonly Channel<Guid> _pending;
    private readonly AutomationOptions _options;
    private readonly TimeProvider _clock;
    private readonly AutomationRouter _router;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _worker;
    private bool _closed;
    public bool HasPendingOperations { get { lock (_gate) return _entries.Values.Any(entry => entry.CompletedAt is null); } }

    internal AutomationOperations(AutomationOptions options, TimeProvider clock, AutomationRouter router)
    {
        options.Validate(); _options = options; _clock = clock; _router = router;
        _pending = Channel.CreateBounded<Guid>(new BoundedChannelOptions(options.MaximumOperations) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        _worker = Task.Run(ProcessAsync);
    }

    internal string Submit(AutomationCommand command)
    {
        lock (_gate)
        {
            if (_closed) throw new AutomationFault(AutomationFailure.Stopping);
            Prune();
            if (command.RequestId is { } request && _requests.TryGetValue(request, out var known))
            {
                if (_entries[known].Command != command) throw new AutomationFault(AutomationFailure.Conflict);
                return known.ToString("D");
            }
            if (_entries.Count >= _options.MaximumOperations) throw new AutomationFault(AutomationFailure.Busy);
            var id = Guid.NewGuid();
            _entries.Add(id, new(command, new(id, "Pending")));
            if (command.RequestId is { } requestId) _requests.Add(requestId, id);
            if (!_pending.Writer.TryWrite(id))
            {
                _entries.Remove(id);
                if (command.RequestId is { } key) _requests.Remove(key);
                throw new AutomationFault(AutomationFailure.Busy);
            }
            return id.ToString("D");
        }
    }

    public OperationSnapshot Get(string id)
    {
        var key = AutomationInput.Id(id);
        lock (_gate) { Prune(); return _entries.TryGetValue(key, out var entry) ? entry.Snapshot : throw new AutomationFault(AutomationFailure.NotFound); }
    }

    private void Prune()
    {
        foreach (var (id, entry) in _entries.Where(item => item.Value.CompletedAt is { } end && _clock.GetUtcNow() - end >= _options.Retention).ToArray())
        {
            _entries.Remove(id);
            if (entry.Command.RequestId is { } request) _requests.Remove(request);
        }
    }

    private async Task ProcessAsync()
    {
        await foreach (var id in _pending.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            Entry entry;
            lock (_gate) { entry = _entries[id]; entry.Snapshot = new(id, "Running"); }
            using var request = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
            request.CancelAfter(_options.CommandTimeout);
            OperationSnapshot result;
            try
            {
                request.Token.ThrowIfCancellationRequested();
                var call = await _router.ExecuteAsync(entry.Command, request.Token).ConfigureAwait(false);
                result = new(id, "Succeeded", callId: call);
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested)
            {
                var failure = _stopping.IsCancellationRequested ? AutomationFailure.Stopping : AutomationFailure.Timeout;
                result = new(id, _stopping.IsCancellationRequested ? "Canceled" : "Failed", failure.ToString(), AutomationFault.MessageFor(failure));
            }
            catch (AutomationFault error) { result = new(id, "Failed", error.Failure.ToString(), error.Message); }
            catch (Exception) { result = new(id, "Failed", AutomationFailure.ProviderUnavailable.ToString(), AutomationFault.MessageFor(AutomationFailure.ProviderUnavailable)); }
            lock (_gate) { entry.Snapshot = result; entry.CompletedAt = _clock.GetUtcNow(); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate) { if (_closed) return; _closed = true; _pending.Writer.TryComplete(); }
        await _stopping.CancelAsync().ConfigureAwait(false);
        await _worker.ConfigureAwait(false);
        _stopping.Dispose();
    }

    private sealed class Entry(AutomationCommand command, OperationSnapshot snapshot)
    {
        internal AutomationCommand Command { get; } = command;
        internal OperationSnapshot Snapshot { get; set; } = snapshot;
        internal DateTimeOffset? CompletedAt { get; set; }
    }
}
