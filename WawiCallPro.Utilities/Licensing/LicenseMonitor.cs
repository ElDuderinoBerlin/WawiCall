using Microsoft.Extensions.Hosting;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Licensing;

/// <summary>Revalidates expiration/offline limits and stops business work after an authoritative rejection.</summary>
public sealed class LicenseMonitor(IProductLicenseService licenses, LicenseProductIdentity product, StartupSettings settings,
    RuntimeFailureState failures, StartupExecutionMode mode, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!product.Required || mode.SuppressNetwork) return;
        var nextOnlineCheck = clock.GetUtcNow().AddSeconds(settings.LicenseCheckIntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            var snapshot = await licenses.ValidateAsync(stoppingToken).ConfigureAwait(false);
            if (snapshot.Status is LicenseStatus.Valid or LicenseStatus.Expired or LicenseStatus.OnlineCheckRequired && snapshot.Online
                && settings.LicenseServerUrl is not null && (clock.GetUtcNow() >= nextOnlineCheck || snapshot.Status != LicenseStatus.Valid))
            {
                nextOnlineCheck = clock.GetUtcNow().AddSeconds(settings.LicenseCheckIntervalSeconds);
                try { snapshot = await licenses.RefreshAsync(stoppingToken).ConfigureAwait(false); }
                catch (LicenseAccessException exception) when (exception.Status == LicenseStatus.ServerUnavailable) { }
            }
            if (snapshot.Status != LicenseStatus.Valid)
            {
                failures.Report(new LicenseAccessException(snapshot.Status));
                return;
            }
        }
    }
}
