using Microsoft.Extensions.Hosting;
using DevExpress.XtraEditors;
using Quantarix.DevExpressExtensions.Licensing;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Interfaces.Hosting;

namespace WawiCallPro.App.Hosting;

internal interface IAppLicensePrompt
{
    Task<string?> RequestKeyAsync(string machineId, CancellationToken cancellationToken);
    Task<string?> RetryKeyAsync(string machineId, string previousKey, LicenseStatus status, CancellationToken cancellationToken);
}

internal sealed class AppLicensePrompt(IUiDispatcher dispatcher, StartupSplash splash, AppUiState state) : IAppLicensePrompt
{
    public Task<string?> RequestKeyAsync(string machineId, CancellationToken cancellationToken) =>
        ShowAsync(machineId, "", null, cancellationToken);

    public Task<string?> RetryKeyAsync(string machineId, string previousKey, LicenseStatus status, CancellationToken cancellationToken) =>
        ShowAsync(machineId, previousKey, status, cancellationToken);

    private async Task<string?> ShowAsync(string machineId, string previousKey, LicenseStatus? failure, CancellationToken cancellationToken)
    {
        await splash.CloseAsync();
        string? key = null;
        await dispatcher.InvokeAsync(() =>
        {
            if (failure is { } status)
            {
                var message = status == LicenseStatus.ServerUnavailable
                    ? "Der Lizenzserver ist derzeit nicht erreichbar. Möchten Sie die Aktivierung erneut versuchen?"
                    : "Die Lizenz konnte nicht aktiviert werden. Möchten Sie die Lizenznummer prüfen und es erneut versuchen?";
                if (XtraMessageBox.Show(message, "WawiCallPro – Lizenzaktivierung", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning)
                    != DialogResult.Retry) return;
            }
            using var dialog = new FrmLicenseKeyInput { MachineKey = machineId, LicenseKey = previousKey };
            dialog.Text = "WawiCallPro – Lizenzaktivierung";
            state.AttachWindow(dialog, "FrmLicenseKeyInput");
            key = dialog.ShowDialog() == DialogResult.OK ? dialog.LicenseKey : null;
        }, cancellationToken);
        return key;
    }
}

/// <summary>Starts before the product plugin or background workers; diagnostic/audit mode never claims a valid license.</summary>
internal sealed class LicenseStartupGate(IProductLicenseService licenses, LicenseProductIdentity product, StartupSettings settings,
    StartupExecutionMode mode, IAppLicensePrompt prompt) : IHostedService
{
    internal AppAutomationState? AutomationState { get; set; }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (mode.SuppressNetwork || !product.Required && !product.Demo) return;
        var snapshot = await licenses.ValidateAsync(cancellationToken);
        if (product.Demo)
        {
            if (snapshot.Status != LicenseStatus.Valid) throw new LicenseAccessException(snapshot.Status);
            return;
        }
        if (product.OnlineActivation && snapshot.Status is LicenseStatus.Expired or LicenseStatus.OnlineCheckRequired
            && snapshot.Online && settings.LicenseServerUrl is not null)
        {
            try { snapshot = await licenses.RefreshAsync(cancellationToken); }
            catch (LicenseAccessException exception) when (exception.Status == LicenseStatus.ServerUnavailable) { }
        }
        if (snapshot.Status != LicenseStatus.Valid)
        {
            if (mode.ComActivated && AutomationState?.UserMode != true) throw new LicenseAccessException(snapshot.Status);
            if (!product.OnlineActivation || settings.LicenseServerUrl is null) throw new LicenseAccessException(snapshot.Status);
            var machineId = await Task.Run(licenses.GetMachineId, cancellationToken);
            var key = await prompt.RequestKeyAsync(machineId, cancellationToken);
            var failure = snapshot.Status;
            while (key is not null)
            {
                try
                {
                    snapshot = await licenses.ActivateAsync(key, cancellationToken);
                    if (snapshot.Status == LicenseStatus.Valid) break;
                    failure = snapshot.Status;
                }
                catch (LicenseAccessException error) when (error.Status is LicenseStatus.Invalid or LicenseStatus.ServerUnavailable)
                {
                    failure = error.Status;
                }
                key = await prompt.RetryKeyAsync(machineId, key, failure, cancellationToken);
            }
            if (key is null) throw new LicenseAccessException(failure);
        }
        if (product.OnlineActivation && snapshot.Status == LicenseStatus.Valid && snapshot.Online && settings.LicenseServerUrl is not null)
        {
            try { snapshot = await licenses.RefreshAsync(cancellationToken); }
            catch (LicenseAccessException exception) when (exception.Status == LicenseStatus.ServerUnavailable) { }
        }
        if (snapshot.Status != LicenseStatus.Valid) throw new LicenseAccessException(snapshot.Status);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
