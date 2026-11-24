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
    private readonly PageOperation _dtmfOperation;
    private readonly PageOperation _audioOperation;
    private readonly Queue<char> _dtmfQueue = new();
    private bool _dtmfOpen;
    private bool _recordingBusy;
    private LiveRecordingState _displayedRecordingState;
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
        _dtmfOperation = new(this, dtmfStatus);
        _audioOperation = new(this, statusLabel);
        dtmfKeypad.DigitPressed += DtmfDigit_Pressed;
        UpdateButtons();
    }

    internal void Bind(ITelephonyService? telephony, CallSnapshot call)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (_busy || _audioOperation.IsRunning || _stopTask is not null) throw new InvalidOperationException("Call controls are busy or stopping.");
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
        if (!CanSendDtmf) _dtmfQueue.Clear();
        dtmfButton.Enabled = dtmfKeypad.Enabled = CanSendDtmf;
        UpdateRecording();
        UpdateAudio();
    }

    private bool CanSendDtmf => !_disposed && _stopTask is null && !_lifetime.IsCancellationRequested
        && _telephony is not null && _call?.State == CallState.Connected;

    private void Dtmf_Click(object? sender, EventArgs e)
    {
        if (!CanSendDtmf) return;
        _dtmfOpen = !_dtmfOpen;
        dtmfKeypad.Visible = dtmfStatus.Visible = _dtmfOpen;
        if (_dtmfOpen) dtmfKeypad.FocusFirstKey();
    }

    private async void DtmfDigit_Pressed(char digit) => await SendDtmfAsync(digit);

    internal Task SendDtmfAsync(char digit)
    {
        if (!CanSendDtmf || !_dtmfOpen || _dtmfQueue.Count >= 32 || !"0123456789*#ABCD".Contains(digit))
            return Task.CompletedTask;
        _dtmfQueue.Enqueue(digit);
        if (_dtmfOperation.IsRunning) return Task.CompletedTask;
        var callId = _call!.Id;
        var service = _telephony!;
        return _dtmfOperation.RunAsync(async token =>
        {
            try
            {
                while (CanSendDtmf && _call?.Id == callId && _dtmfQueue.TryDequeue(out var next))
                    await service.SendDtmfAsync(callId, next, token);
                dtmfStatus.Text = "";
            }
            finally { _dtmfQueue.Clear(); }
        });
    }
    private void UpdateRecording()
    {
        if (_disposed) return;
        var recordings = _telephony as ILiveCallRecordingService;
        var snapshot = _call is null ? null : recordings?.GetRecording(_call.Id);
        var state = snapshot?.State ?? LiveRecordingState.Idle;
        _displayedRecordingState = state;
        recordingButton.Text = state switch
        {
            LiveRecordingState.Starting => "Aufnahme abbrechen",
            LiveRecordingState.Recording => "Aufnahme stoppen",
            _ => "Aufnahme starten"
        };
        recordingButton.Enabled = !_recordingBusy && _stopTask is null && recordings is not null
            && (_call?.State == CallState.Connected && state is LiveRecordingState.Idle or LiveRecordingState.Completed or LiveRecordingState.Failed
                || _call?.State is CallState.Connected or CallState.Held && state is LiveRecordingState.Starting or LiveRecordingState.Recording);
        recordingStatus.Text = recordings is null ? "Aufnahme nicht verfügbar." : state switch
        {
            LiveRecordingState.Starting => "Aufnahme wird vorbereitet …",
            LiveRecordingState.Recording => "Aufnahme läuft · beide Gesprächsseiten",
            LiveRecordingState.Stopping => "Aufnahme wird abgeschlossen …",
            LiveRecordingState.Completed => "Aufnahme gespeichert · Stereo: eigene Stimme links, Gegenstelle rechts.",
            LiveRecordingState.Failed => WawiCallPro.Utilities.Operations.LiveRecordingMessages.Failure(snapshot?.Failure),
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
        var stop = _displayedRecordingState is LiveRecordingState.Starting or LiveRecordingState.Recording;
        return _recordingOperation.RunAsync(async token =>
        {
            _recordingBusy = true; recordingButton.Enabled = false;
            try
            {
                var current = recordings.GetRecording(id).State;
                if (stop && current is (LiveRecordingState.Starting or LiveRecordingState.Recording))
                    await recordings.StopLiveRecordingAsync(id, token);
                else if (!stop && current is (LiveRecordingState.Idle or LiveRecordingState.Completed or LiveRecordingState.Failed))
                    await recordings.StartLiveRecordingAsync(id, token);
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
        await Task.WhenAll(_operation.StopAsync(), _recordingOperation.StopAsync(), _dtmfOperation.StopAsync(), _audioOperation.StopAsync());
    }
    private void DisposePage()
    {
        if (_disposed) return;
        _disposed = true;
        if (_telephony is not null) _telephony.CallChanged -= Call_Changed;
        if (_telephony is ILiveCallRecordingService recordings) recordings.RecordingChanged -= Recording_Changed;
        _lifetime.Cancel(); _lifetime.Dispose(); _operation.Dispose();
        _recordingOperation.Dispose();
        _dtmfOperation.Dispose();
        _audioOperation.Dispose();
    }
}
