using Microsoft.Extensions.Hosting;
using Quantarix.DevExpressExtensions.Licensing;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Interfaces.Hosting;

namespace WawiCallPro.App.Hosting;

internal interface IAppLicensePrompt
{
    Task<string?> RequestKeyAsync(string machineId, CancellationToken cancellationToken);
}

internal sealed class AppLicensePrompt(IUiDispatcher dispatcher, StartupSplash splash) : IAppLicensePrompt
{
    public async Task<string?> RequestKeyAsync(string machineId, CancellationToken cancellationToken)
    {
        await splash.CloseAsync();
        string? key = null;
        await dispatcher.InvokeAsync(() =>
        {
            using var dialog = new FrmLicenseKeyInput { MachineKey = machineId };
            dialog.Text = "WawiCallPro – Lizenzaktivierung";
            key = dialog.ShowDialog() == DialogResult.OK ? dialog.LicenseKey : null;
        }, cancellationToken);
        return key;
    }
}

/// <summary>Starts before the product plugin or background workers; diagnostic/audit mode never claims a valid license.</summary>
internal sealed class LicenseStartupGate(IProductLicenseService licenses, LicenseProductIdentity product, StartupSettings settings,
    StartupExecutionMode mode, IAppLicensePrompt prompt) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (mode.SuppressNetwork || !product.Required) return;
        var snapshot = await licenses.ValidateAsync(cancellationToken);
        if (snapshot.Status is LicenseStatus.Expired or LicenseStatus.OnlineCheckRequired && snapshot.Online && settings.LicenseServerUrl is not null)
        {
            try { snapshot = await licenses.RefreshAsync(cancellationToken); }
            catch (LicenseAccessException exception) when (exception.Status == LicenseStatus.ServerUnavailable) { }
        }
        if (snapshot.Status != LicenseStatus.Valid)
        {
            if (settings.LicenseServerUrl is null) throw new LicenseAccessException(snapshot.Status);
            var machineId = await Task.Run(licenses.GetMachineId, cancellationToken);
            var key = await prompt.RequestKeyAsync(machineId, cancellationToken);
            if (key is null) throw new LicenseAccessException(snapshot.Status);
            snapshot = await licenses.ActivateAsync(key, cancellationToken);
        }
        if (snapshot.Status == LicenseStatus.Valid && snapshot.Online && settings.LicenseServerUrl is not null)
        {
            try { snapshot = await licenses.RefreshAsync(cancellationToken); }
            catch (LicenseAccessException exception) when (exception.Status == LicenseStatus.ServerUnavailable) { }
        }
        if (snapshot.Status != LicenseStatus.Valid) throw new LicenseAccessException(snapshot.Status);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
