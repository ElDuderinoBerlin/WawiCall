using System.Collections.Immutable;
using WawiCallPro.Interfaces.History;

namespace WawiCallPro.App.Hosting;

/// <summary>Creates call-bound UI sessions; the owning call or history page stops and disposes them.</summary>
public sealed class CallNotesSessionFactory(ICallHistoryService history)
{
    internal CallNotesSession Create(Guid callId) => new(history, callId);
}

/// <summary>Shared by the popup and call page on the UI thread, independently of the current ERP selection.</summary>
internal sealed class CallNotesSession(ICallHistoryService history, Guid callId) : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private Guid _draftId = Guid.NewGuid();
    private Task _running = Task.CompletedTask;
    private Task? _stopTask;
    private bool _stopping;
    private bool _saving;
    private bool _disposed;
    internal event EventHandler? Changed;
    internal Guid CallId { get; } = callId;
    internal string Draft { get; private set; } = "";
    internal ImmutableArray<CallNote> Notes { get; private set; } = [];
    internal WawiCallPro.Interfaces.Telephony.CallSnapshot? StoredCall { get; private set; }
    internal string Status { get; private set; } = "Notizen gehören zu diesem Gespräch, unabhängig von der Firmenzuordnung.";
    internal bool IsBusy { get; private set; }
    internal bool IsStopping => _stopping;
    internal Task PendingOperation => _running;
    internal bool HasUnsavedChanges => Draft.Length != 0 || IsBusy && _saving;

    internal void SetDraft(string text)
    {
        if (IsBusy || _stopping || _disposed || Draft == text) return;
        Draft = text;
        Status = Draft.Length == 0 ? "Kein ungespeicherter Entwurf." : "Ungespeicherter Entwurf für dieses Gespräch.";
        Publish();
    }

    internal void DiscardDraft()
    {
        if (IsBusy || _stopping || _disposed) return;
        _draftId = Guid.NewGuid();
        SetDraft("");
    }

    internal Task LoadAsync() => RunAsync(async token =>
    {
        var stored = await history.GetCallAsync(CallId, token);
        token.ThrowIfCancellationRequested();
        if (stored is not null && (stored.Call.Id != CallId || stored.Notes.Any(note => note.CallId != CallId)))
            throw new InvalidOperationException("Unexpected call identity.");
        StoredCall = stored?.Call;
        Notes = stored?.Notes.OrderBy(note => note.CreatedAtUtc).ThenBy(note => note.Id).ToImmutableArray() ?? [];
        Status = stored is null ? "Gespräch noch nicht in der Historie verfügbar. Der Entwurf bleibt erhalten; bitte später erneut versuchen."
            : "Gesprächsnotizen geladen. Ein vorhandener Entwurf bleibt erhalten.";
    }, false);

    internal Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Draft)) return Task.CompletedTask;
        var text = Draft;
        var id = _draftId; // Preserve the ID after errors, including a lost acknowledgement of a committed write.
        return RunAsync(async token =>
        {
            var note = await history.SaveNoteAsync(CallId, id, text, token);
            token.ThrowIfCancellationRequested();
            if (note.CallId != CallId || note.Id != id) throw new InvalidOperationException("Unexpected note identity.");
            Notes = Notes.Where(existing => existing.Id != id).Append(note).OrderBy(existing => existing.CreatedAtUtc).ThenBy(existing => existing.Id).ToImmutableArray();
            Draft = "";
            _draftId = Guid.NewGuid();
            Status = "Gesprächsnotiz gespeichert.";
        }, true);
    }

    private Task RunAsync(Func<CancellationToken, Task> action, bool saving)
    {
        if (IsBusy || _stopping || _disposed) return _running;
        IsBusy = true; _saving = saving;
        Status = saving ? "Notiz wird gespeichert …" : "Notizen werden geladen …";
        Publish();
        _running = RunCoreAsync(action);
        return _running;
    }
    private async Task RunCoreAsync(Func<CancellationToken, Task> action)
    {
        try { await action(_lifetime.Token); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) { if (!_stopping) Status = PageOperation.Describe(exception) + " Der Entwurf bleibt erhalten."; }
        finally { IsBusy = false; _saving = false; Publish(); }
    }
    private void Publish() { if (!_disposed) Changed?.Invoke(this, EventArgs.Empty); }
    internal Task StopAsync() => _stopTask ??= StopCoreAsync();
    private async Task StopCoreAsync()
    {
        _stopping = true; Publish();
        await _lifetime.CancelAsync();
        await _running;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _stopping = true;
        _lifetime.Cancel(); _lifetime.Dispose(); Changed = null;
    }
}

internal sealed class UnsavedCallNotesException() : Exception("Ungespeicherte Gesprächsnotizen vorhanden. Bitte Notizen speichern oder die Entwürfe leeren beziehungsweise verwerfen.");
