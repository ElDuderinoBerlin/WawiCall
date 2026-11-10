using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.App;

public partial class MainForm
{
    private readonly CancellationTokenSource _telephonyUiLifetime = new();
    private void BindTelephony()
    {
        if (_workspace?.Telephony is null) return;
        _workspace.Telephony.RegistrationChanged += TelephonyRegistration_Changed;
        _workspace.Telephony.CallChanged += TelephonyCall_Changed;
        UpdateTelephonyStatus();
        foreach (var call in _workspace.Telephony.GetCalls()) QueueCall(call);
    }
    private void UnbindTelephony()
    {
        if (_telephonyUiLifetime.IsCancellationRequested) return;
        if (_workspace?.Telephony is not null)
        {
            _workspace.Telephony.RegistrationChanged -= TelephonyRegistration_Changed;
            _workspace.Telephony.CallChanged -= TelephonyCall_Changed;
        }
        _telephonyUiLifetime.Cancel(); _telephonyUiLifetime.Dispose();
    }
    private async void TelephonyRegistration_Changed(object? sender, RegistrationChangedEventArgs e)
    {
        if (!IsHandleCreated || IsDisposed) return;
        try { await InvokeAsync(UpdateTelephonyStatus, _telephonyUiLifetime.Token); }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }
    private async void TelephonyCall_Changed(object? sender, CallChangedEventArgs e)
    {
        if (!IsHandleCreated || IsDisposed) return;
        try
        {
            await InvokeAsync(() => { AppendOutput("Gesprächsstatus: " + (e.Call.State switch
            {
                CallState.Created => "Neues Gespräch", CallState.Dialing => "Rufaufbau", CallState.Ringing => "Klingelt",
                CallState.Connected => "Verbunden", CallState.Held => "Wird gehalten", CallState.Ended => "Beendet",
                _ => "Fehlgeschlagen"
            })); QueueCall(e.Call); }, _telephonyUiLifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }
    private void UpdateTelephonyStatus()
    {
        if (_databaseBusy) return;
        connectionLabel.Text = _workspace?.Telephony?.Registration.State switch
        {
            RegistrationState.Registered => "SIP-Konto angemeldet.",
            RegistrationState.Registering => "SIP-Anmeldung läuft …",
            RegistrationState.Reconnecting => "SIP-Verbindung unterbrochen; erneute Anmeldung …",
            RegistrationState.Failed => "SIP-Anmeldung fehlgeschlagen. Einstellungen prüfen.",
            _ => "SIP-Konto nicht angemeldet."
        };
        foreach (var document in _documents.Values)
            if (document.Control is Panel { Controls.Count: > 0 } host && host.Controls[0] is HomeDashboard dashboard)
                dashboard.RefreshStatus();
    }
}
