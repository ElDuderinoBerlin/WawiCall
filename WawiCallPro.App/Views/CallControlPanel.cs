using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.App.Views;

/// <summary>Commands belong to one stable call ID; only observed service states enable subsequent actions.</summary>
public partial class CallControlPanel : XtraUserControl
{
    private ITelephonyService? _telephony;
    private CallSnapshot? _call;
    private readonly PageOperation _operation;
    private readonly PageOperation _recordingOperation;
    private bool _recordingBusy;
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _stopTask;
    private bool _busy;
    private bool _disposed;

    public CallControlPanel()
    {
        InitializeComponent();
        UiButtonSizing.Attach(this);
        _operation = new(this, statusLabel);
        _recordingOperation = new(this, recordingStatus);
        UpdateButtons();
    }

    internal void Bind(ITelephonyService? telephony, CallSnapshot call)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (_busy || _stopTask is not null) throw new InvalidOperationException("Call controls are busy or stopping.");
        if (_telephony is not null) _telephony.CallChanged -= Call_Changed;
        if (_telephony is ILiveCallRecordingService previous) previous.RecordingChanged -= Recording_Changed;
        _telephony = telephony; _call = call;
        if (_telephony is not null) _telephony.CallChanged += Call_Changed;
        if (_telephony is ILiveCallRecordingService recordings) recordings.RecordingChanged += Recording_Changed;
        ShowStatus(); UpdateButtons();
    }

    internal void Observe(CallSnapshot call)
    {
        if (_disposed || _stopTask is not null || _call is null || call.Id != _call.Id || call.Revision < _call.Revision) return;
        if (IsEnded(_call) && !IsEnded(call)) return;
        _call = call;
        ShowStatus(); UpdateButtons();
    }

    private static bool IsEnded(CallSnapshot call) => call.State is CallState.Ended or CallState.Failed;
    private bool IsIncomingRinging => _call is { Direction: CallDirection.Incoming, State: CallState.Ringing };
    private void ShowStatus()
    {
        statusLabel.Text = _telephony is null ? "Telefonie nicht verfügbar." : _call?.State switch
        {
            CallState.Created => "Neues Gespräch", CallState.Dialing => "Rufaufbau …", CallState.Ringing => "Klingelt",
            CallState.Connected => "Verbunden", CallState.Held => "Gespräch wird gehalten",
            CallState.Ended => "Gespräch beendet", CallState.Failed => "Gespräch fehlgeschlagen", _ => "Kein Gespräch"
        };
    }
    private void UpdateButtons()
    {
        if (_disposed) return;
        var available = !_busy && _stopTask is null && !_lifetime.IsCancellationRequested && _telephony is not null && _call is not null && !IsEnded(_call);
        answerButton.Enabled = available && IsIncomingRinging;
        endButton.Text = IsIncomingRinging ? "Ablehnen" : "Auflegen";
        endButton.Enabled = available && (IsIncomingRinging || _call!.Direction == CallDirection.Outgoing
            || _call.State is CallState.Connected or CallState.Held);
        holdButton.Text = _call?.State == CallState.Held ? "Fortsetzen" : "Halten";
        holdButton.Enabled = available && (_call!.State is CallState.Connected or CallState.Held);
        UpdateRecording();
    }
    private void UpdateRecording()
    {
        if (_disposed) return;
        var recordings = _telephony as ILiveCallRecordingService;
        var state = _call is null ? LiveRecordingState.Idle : recordings?.GetRecording(_call.Id).State ?? LiveRecordingState.Idle;
        recordingButton.Text = state == LiveRecordingState.Recording ? "Aufnahme stoppen" : "Aufnahme starten";
        recordingButton.Enabled = !_recordingBusy && _stopTask is null && recordings is not null
            && (_call?.State == CallState.Connected && state is LiveRecordingState.Idle or LiveRecordingState.Completed or LiveRecordingState.Failed
                || _call?.State is CallState.Connected or CallState.Held && state == LiveRecordingState.Recording);
        recordingStatus.Text = recordings is null ? "Aufnahme nicht verfügbar." : state switch
        {
            LiveRecordingState.Starting => "Aufnahme wird vorbereitet …",
            LiveRecordingState.Recording => "Aufnahme läuft · beide Gesprächsseiten",
            LiveRecordingState.Stopping => "Aufnahme wird abgeschlossen …",
            LiveRecordingState.Completed => "Aufnahme gespeichert · Stereo: eigene Stimme links, Gegenstelle rechts.",
            LiveRecordingState.Failed => "Aufnahme fehlgeschlagen. Audiosignal, Geräte und Speicher prüfen.",
            _ => "Keine Aufnahme"
        };
        UiButtonSizing.Apply(recordingButton);
    }
    private async void Recording_Changed(object? sender, LiveRecordingChangedEventArgs e)
    {
        if (_disposed || _stopTask is not null || !IsHandleCreated || _call?.Id != e.Snapshot.CallId) return;
        try { await InvokeAsync(UpdateRecording, _lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }
    private async void Recording_Click(object? sender, EventArgs e) => await ToggleRecordingAsync();
    internal Task ToggleRecordingAsync()
    {
        if (!recordingButton.Enabled || _call is null || _telephony is not ILiveCallRecordingService recordings) return Task.CompletedTask;
        var id = _call.Id;
        return _recordingOperation.RunAsync(async token =>
        {
            _recordingBusy = true; recordingButton.Enabled = false;
            try
            {
                if (recordings.GetRecording(id).State == LiveRecordingState.Recording)
                    await recordings.StopLiveRecordingAsync(id, token);
                else await recordings.StartLiveRecordingAsync(id, token);
            }
            finally { _recordingBusy = false; UpdateRecording(); }
        });
    }
    private async void Call_Changed(object? sender, CallChangedEventArgs e)
    {
        if (_disposed || _stopTask is not null || !IsHandleCreated) return;
        try { await InvokeAsync(() => Observe(e.Call), _lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }
    private async Task CommandAsync(Func<ITelephonyService, Guid, CancellationToken, Task> command)
    {
        if (_disposed || _busy || _stopTask is not null || _call is null || IsEnded(_call) || _telephony is null) return;
        var id = _call.Id;
        await _operation.RunAsync(async token =>
        {
            _busy = true; UpdateButtons();
            try
            {
                await command(_telephony, id, token);
                if (_disposed || _lifetime.IsCancellationRequested) return;
                if (_telephony.GetCalls().FirstOrDefault(call => call.Id == id) is { } observed) Observe(observed);
                ShowStatus();
            }
            finally { _busy = false; UpdateButtons(); }
        });
    }
    private async void Answer_Click(object? sender, EventArgs e)
    { if (IsIncomingRinging) await CommandAsync((service, id, token) => service.AnswerAsync(id, token)); }
    private async void End_Click(object? sender, EventArgs e)
    {
        if (!endButton.Enabled) return;
        if (IsIncomingRinging) await CommandAsync((service, id, token) => service.RejectAsync(id, token));
        else await CommandAsync((service, id, token) => service.HangUpAsync(id, token));
    }
    private async void Hold_Click(object? sender, EventArgs e)
    {
        if (_call?.State == CallState.Held) await CommandAsync((service, id, token) => service.ResumeAsync(id, token));
        else if (_call?.State == CallState.Connected) await CommandAsync((service, id, token) => service.HoldAsync(id, token));
    }

    internal Task StopAsync() => _stopTask ??= StopCoreAsync();
    private async Task StopCoreAsync()
    {
        if (_telephony is not null) _telephony.CallChanged -= Call_Changed;
        if (_telephony is ILiveCallRecordingService recordings) recordings.RecordingChanged -= Recording_Changed;
        await _lifetime.CancelAsync();
        UpdateButtons();
        await _operation.StopAsync();
        await _recordingOperation.StopAsync();
    }
    private void DisposePage()
    {
        if (_disposed) return;
        _disposed = true;
        if (_telephony is not null) _telephony.CallChanged -= Call_Changed;
        if (_telephony is ILiveCallRecordingService recordings) recordings.RecordingChanged -= Recording_Changed;
        _lifetime.Cancel(); _lifetime.Dispose(); _operation.Dispose();
        _recordingOperation.Dispose();
    }
}
