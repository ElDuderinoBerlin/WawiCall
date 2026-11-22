using Microsoft.Extensions.Hosting;
using System.Threading.Channels;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.App.Hosting;

/// <summary>Persists detached caller decisions independently of forms. SIP remains the owner of call-state persistence.</summary>
internal sealed class AppCallAssociation(
    ITelephonyService phone,
    ICallHistoryService history,
    IPhoneSearchIndexService index,
    ICallerMatchDecisionService decisions,
    IPhoneNumberInterpreter interpreter,
    ICallAssociationService associations,
    AppTelephonyAccess access,
    ILogWindowService log,
    TimeProvider clock) : IHostedService, IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Work> _pending = [];
    private readonly HashSet<Guid> _completed = [];
    private readonly Queue<Guid> _completionOrder = [];
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
    private readonly CancellationTokenSource _stopping = new();
    private Task _running = Task.CompletedTask;
    private Task? _stop;
    private int _disposed;
    internal int PendingCount { get { lock (_gate) return _pending.Count; } }
    public Task StartAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        phone.CallChanged += Changed;
        foreach (var call in phone.GetCalls()) Enqueue(call);
        _running = Task.Run(RunAsync); return Task.CompletedTask;
    }
    private void Changed(object? sender, CallChangedEventArgs args) => Enqueue(args.Call);
    private void Enqueue(CallSnapshot call)
    {
        lock (_gate)
        {
            if (_stop is not null || _stopping.IsCancellationRequested || _completed.Contains(call.Id) || _pending.ContainsKey(call.Id)) return;
            if (_pending.Count >= 128) { log.LogWarning("Die automatische Anrufzuordnung ist ausgelastet. Gespeicherte Gespräche bleiben über die Historie erreichbar."); return; }
            _pending.Add(call.Id, new(call, (phone as ICallLineContextProvider)?.GetLineContext(call.Id)));
        }
        _wake.Writer.TryWrite(true);
    }
    private async Task RunAsync()
    {
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                while (_wake.Reader.TryRead(out _)) { }
                Work[] work; lock (_gate) work = _pending.Values.ToArray();
                foreach (var entry in work)
                {
                    try
                    {
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15), clock);
                        using var request = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, _stopping.Token);
                        if (await AssociateAsync(entry, request.Token).ConfigureAwait(false))
                        {
                            lock (_gate)
                            {
                                _pending.Remove(entry.Call.Id); _completed.Add(entry.Call.Id); _completionOrder.Enqueue(entry.Call.Id);
                                if (_completionOrder.Count > 128) _completed.Remove(_completionOrder.Dequeue());
                            }
                        }
                    }
                    catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { throw; }
                    catch (Exception)
                    {
                        if (!entry.Warned) { entry.Warned = true; log.LogWarning("Die automatische Anrufzuordnung wartet auf Datenbank, Freigabe oder Suchstand. Der Gesprächsstatus wird weiterhin gespeichert."); }
                    }
                }
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
                var delay = Task.Delay(TimeSpan.FromSeconds(1), clock, wait.Token);
                var signal = _wake.Reader.ReadAsync(wait.Token).AsTask();
                await Task.WhenAny(delay, signal).ConfigureAwait(false); await wait.CancelAsync().ConfigureAwait(false);
                try { await Task.WhenAll(delay, signal).ConfigureAwait(false); }
                catch (OperationCanceledException) when (wait.IsCancellationRequested) { }
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
    }
    private async Task<bool> AssociateAsync(Work entry, CancellationToken token)
    {
        // A late SIP publication or another UI must never replace already captured historical evidence.
        var stored = await history.GetCallAsync(entry.Call.Id, token).ConfigureAwait(false);
        if (stored is null) return false;
        if (stored.Lookup is not null) return true;
        var incoming = interpreter.InterpretIncoming(entry.Call.RemoteAddress, entry.Line);
        if (incoming.Kind != PhoneNumberAddressKind.Public) return true;
        await access.CheckLicenseAsync(token).ConfigureAwait(false); access.CheckBusinessSource();
        await index.RefreshAsync(token).ConfigureAwait(false);
        var result = index.Find(incoming, token);
        var decision = RespectDialCompany(decisions.Evaluate(result, new(entry.Call.Company)), entry.Call);
        var current = index.GetStatus();
        if (current.Generation != decision.IndexGeneration || decision.Selection is not null && !current.IsComplete) return false;
        await access.CheckLicenseAsync(token).ConfigureAwait(false); access.CheckBusinessSource();
        await associations.SaveAsync(entry.Call.Id, decision, token).ConfigureAwait(false);
        return true;
    }
    internal static CallerMatchDecision RespectDialCompany(CallerMatchDecision decision, CallSnapshot call) =>
        call.Direction == CallDirection.Outgoing && call.Company is { } company && decision.Selection is { Kind: not CallerMatchSelectionKind.Manual } selected
        && selected.Party.Company != company ? decision with { Kind = CallerMatchDecisionKind.LineConflict, Selection = null,
            Lookup = decision.Lookup with { SelectionReason = null } } : decision;
    public Task StopAsync(CancellationToken token)
    {
        lock (_gate) return _stop ??= StopCoreAsync();
    }
    private async Task StopCoreAsync()
    {
        phone.CallChanged -= Changed;
        await _stopping.CancelAsync().ConfigureAwait(false);
        await _running.ConfigureAwait(false);
        lock (_gate) _pending.Clear();
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await StopAsync(default).ConfigureAwait(false); _stopping.Dispose();
    }
    private sealed class Work(CallSnapshot call, PhoneLineContext? line)
    { internal CallSnapshot Call { get; } = call; internal PhoneLineContext? Line { get; } = line; internal bool Warned; }
}
