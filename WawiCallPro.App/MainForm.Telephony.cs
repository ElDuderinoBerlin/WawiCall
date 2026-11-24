using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Licensing;

namespace WawiCallPro.App;

public partial class MainForm
{
    internal FrmDial CreateDialDialog()
    {
        var dialog = _workspace?.Telephony is { } telephony
            ? new FrmDial(_workspace.Directory, telephony, _workspace.Companies.Current.Company?.Key) : new FrmDial();
        _state?.AttachWindow(dialog, "FrmDial");
        if (_workspace is not null) dialog.ConfigureCompanyScope(_workspace.Settings);
        dialog.ConfigurePhoneSearch(_workspace?.PartySearch, _state);
        return dialog;
    }
    private void Dial_Click(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
    {
        if (_closing) return;
        if (_workspace?.CanUseBusinessDirectory != true && _workspace?.Telephony?.GetCalls().Any(call => call.State == CallState.Connected) != true)
        { UpdateBusinessActions(); return; }
        if (OwnedForms.OfType<FrmDial>().FirstOrDefault(dialog => !dialog.IsDisposed) is { } existing)
        { existing.Show(); if (!_backgroundRun) existing.Activate(); return; }
        using var waiting = new Hosting.UiWaitCursor(this);
        var dial = CreateDialDialog();
        dial.SelectPhoneSearch();
        if (_backgroundRun) dial.PrepareForBackgroundRun();
        dial.Show(this);
    }
    private readonly CancellationTokenSource _telephonyUiLifetime = new();
    private bool _startingSipRegistration;

    private async void SipConnect_Click(object sender, DevExpress.XtraBars.ItemClickEventArgs e) => await ConnectSavedSipProfileAsync();

    private async Task ConnectSavedSipProfileAsync()
    {
        if (_closing || _startingSipRegistration || !sipConnectItem.Enabled ||
            _workspace?.TelephonyConnection is not { } connection) return;
        _startingSipRegistration = true;
        sipConnectItem.Enabled = false;
        using var waiting = new Hosting.UiWaitCursor(this);
        try { await connection.ConnectAsync(_telephonyUiLifetime.Token); }
        catch (OperationCanceledException) when (_telephonyUiLifetime.IsCancellationRequested) { }
        catch (TelephonyException error)
        {
            // The SIP runtime already reports failures that reached the registrar.
            if (error.Code == "Busy" && !_closing && !IsDisposed)
                AppendOutput("Die SIP-Anmeldung ist erst nach Ende des laufenden Gesprächs möglich.");
        }
        catch (Exception error) when (error is LicenseAccessException or UnauthorizedAccessException)
        {
            if (!_closing && !IsDisposed) AppendOutput("Die SIP-Anmeldung konnte nicht freigegeben werden. Prüfen Sie den Lizenz- und Anwendungsstatus.");
        }
        catch (Exception)
        {
            if (!_closing && !IsDisposed) AppendOutput("Die SIP-Anmeldung konnte nicht gestartet werden. Bitte prüfen Sie das gespeicherte SIP-Profil.");
        }
        finally
        {
            _startingSipRegistration = false;
            if (!_closing && !IsDisposed) UpdateTelephonyStatus();
        }
    }

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
            })); QueueCall(e.Call); UpdateBusinessActions(); }, _telephonyUiLifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }
    private void UpdateTelephonyStatus()
    {
        sipConnectItem.Enabled = !_startingSipRegistration && _workspace?.TelephonyConnection is not null
            && _workspace.Telephony?.Registration.State is RegistrationState.Stopped or RegistrationState.Failed;
        var text = _workspace?.Telephony?.Registration.State switch
        {
            RegistrationState.Registered => "SIP-Konto angemeldet.",
            RegistrationState.Registering => "SIP-Anmeldung läuft …",
            RegistrationState.Reconnecting => "SIP-Verbindung unterbrochen; erneute Anmeldung …",
            RegistrationState.Failed => "SIP-Anmeldung fehlgeschlagen. Einstellungen prüfen.",
            _ => "SIP-Konto nicht angemeldet."
        };
        telephonyStatusItem.Caption = _workspace?.Telephony?.Registration.State switch
        {
            RegistrationState.Registered => "SIP: Angemeldet",
            RegistrationState.Registering => "SIP: Anmeldung läuft …",
            RegistrationState.Reconnecting => "SIP: Verbindungsaufbau …",
            RegistrationState.Failed => "SIP: Anmeldung fehlgeschlagen",
            _ => "SIP: Nicht angemeldet"
        };
        telephonyStatusItem.Hint = text;
        foreach (var document in _documents.Values)
            if (document.Control is Panel { Controls.Count: > 0 } host && host.Controls[0] is HomeDashboard dashboard)
                dashboard.RefreshStatus();
    }
}
