using Microsoft.Extensions.Hosting;
using Quantarix.DevExpressExtensions.Licensing;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Licensing;

namespace WawiCallPro.App.Hosting;

internal interface IAppLicensePrompt
{
    string? RequestKey(string machineId);
}

internal sealed class AppLicensePrompt : IAppLicensePrompt
{
    public string? RequestKey(string machineId)
    {
        using var dialog = new FrmLicenseKeyInput { MachineKey = machineId };
        dialog.Text = "WawiCallPro – Lizenzaktivierung";
        return dialog.ShowDialog() == DialogResult.OK ? dialog.LicenseKey : null;
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
            var key = prompt.RequestKey(machineId);
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
