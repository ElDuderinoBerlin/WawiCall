using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private readonly CancellationTokenSource _sipUiLifetime = new();

    private async void SipConnect_Click(object? sender, EventArgs e)
    {
        if (_operation is null || _workspace?.TelephonyConnection is null) return;
        await _operation.RunAsync(async token =>
        {
            await SaveAsync(token);
            await _workspace.TelephonyConnection.ConnectAsync(token);
            UpdateSipStatus();
        });
    }

    private async void SipDisconnect_Click(object? sender, EventArgs e)
    {
        if (_operation is null || _workspace?.TelephonyConnection is null) return;
        await _operation.RunAsync(async token =>
        {
            await _workspace.TelephonyConnection.DisconnectAsync(token);
            UpdateSipStatus();
        });
    }

    private async void SipRegistration_Changed(object? sender, RegistrationChangedEventArgs e)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { await InvokeAsync(UpdateSipStatus, _sipUiLifetime.Token); }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    private void UpdateSipStatus()
    {
        var state = _workspace?.Telephony?.Registration.State ?? RegistrationState.Stopped;
        sipStatus.Text = state switch
        {
            RegistrationState.Registering => "Anmeldung läuft …",
            RegistrationState.Registered => "Angemeldet",
            RegistrationState.Reconnecting => "Verbindung unterbrochen; erneute Anmeldung …",
            RegistrationState.Failed => "Anmeldung fehlgeschlagen. Konto und Verbindung prüfen.",
            _ => "Nicht angemeldet"
        };
        sipConnectButton.Enabled = _workspace?.TelephonyConnection is not null && state is RegistrationState.Stopped or RegistrationState.Failed;
        sipDisconnectButton.Enabled = _workspace?.TelephonyConnection is not null && state != RegistrationState.Stopped;
    }
}
