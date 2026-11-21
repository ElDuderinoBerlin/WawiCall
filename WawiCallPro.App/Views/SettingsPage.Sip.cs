using WawiCallPro.Interfaces.Telephony;
using DevExpress.XtraEditors.Controls;
using DevExpress.Utils.Svg;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private readonly CancellationTokenSource _sipUiLifetime = new();
    private static readonly Lazy<SvgImage> PasswordShowIcon = new(() => RibbonIconHelper.Load("PasswordShow"));
    private static readonly Lazy<SvgImage> PasswordHideIcon = new(() => RibbonIconHelper.Load("PasswordHide"));

    private void SetSipPasswordVisibility(bool visible)
    {
        var start = passwordEdit.SelectionStart;
        var length = passwordEdit.SelectionLength;
        passwordEdit.Properties.UseSystemPasswordChar = !visible;
        passwordVisibilityButton.ImageOptions.SvgImage = visible ? PasswordHideIcon.Value : PasswordShowIcon.Value;
        passwordVisibilityButton.ToolTip = visible ? "Kennwort ausblenden" : "Kennwort anzeigen";
        passwordVisibilityButton.Caption = passwordVisibilityButton.ToolTip;
        passwordEdit.SelectionStart = start;
        passwordEdit.SelectionLength = length;
    }

    private void PasswordVisibility_Click(object? sender, ButtonPressedEventArgs e)
    {
        if (!ReferenceEquals(e.Button, passwordVisibilityButton)) return;
        if (passwordEdit.Properties.UseSystemPasswordChar && passwordEdit.Text.Length == 0
            && _snapshot?.Options.SipAccount?.Password is { } stored && _workspace is not null)
        {
            try
            {
                using var secret = _workspace.Secrets.Unprotect(stored, WawiCallPro.Utilities.Configuration.SipAccountOptions.PasswordPurpose);
                passwordEdit.Text = secret.Reveal();
            }
            catch (WawiCallPro.Utilities.Security.SecretProtectionException)
            {
                statusLabel.Text = "Das gespeicherte Kennwort konnte nicht gelesen werden. Bitte erneut eingeben.";
                return;
            }
        }
        SetSipPasswordVisibility(passwordEdit.Properties.UseSystemPasswordChar);
    }

    private async void SipConnect_Click(object? sender, EventArgs e)
    {
        if (_operation is null || _workspace?.TelephonyConnection is null) return;
        await _operation.RunAsync(async token =>
        {
            // Saving reloads the form. Keep the draft available for correction and retry,
            // including failures reported asynchronously after ConnectAsync returns.
            var passwordDraft = passwordEdit.Text;
            try { await SaveAsync(token); }
            finally
            {
                if (!IsDisposed && !token.IsCancellationRequested)
                {
                    passwordEdit.Text = passwordDraft;
                    SetSipPasswordVisibility(false);
                }
            }
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
        var registration = _workspace?.Telephony?.Registration;
        var state = registration?.State ?? RegistrationState.Stopped;
        sipStatus.Text = state switch
        {
            RegistrationState.Registering => "Anmeldung läuft …",
            RegistrationState.Registered => "Angemeldet",
            RegistrationState.Reconnecting => "Verbindung unterbrochen; erneute Anmeldung …",
            RegistrationState.Failed when registration?.ErrorCode == "DatabaseNotReady" =>
                "Anmeldung nicht gestartet: Kommunikationsdatenbank und Arbeitsplatz prüfen.",
            RegistrationState.Failed => "Anmeldung fehlgeschlagen. Konto und Verbindung prüfen.",
            _ => "Nicht angemeldet"
        };
        sipConnectButton.Enabled = _workspace?.TelephonyConnection is not null && state is RegistrationState.Stopped or RegistrationState.Failed;
        sipDisconnectButton.Enabled = _workspace?.TelephonyConnection is not null && state != RegistrationState.Stopped;
    }
}
