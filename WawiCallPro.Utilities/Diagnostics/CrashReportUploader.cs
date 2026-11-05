using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Utilities.Licensing;

namespace WawiCallPro.Utilities.Diagnostics;

public sealed record StartupExecutionMode(bool SuppressNetwork = false);

/// <summary>Uploads only the sanitized persisted payload, with product-bound activation authentication.</summary>
public sealed class CrashReportUploader(CrashReportQueue queue, IProductLicenseService licenses, StartupSettings settings,
    IStartupHttpClientFactory clients, StartupExecutionMode mode) : BackgroundService
{
    private readonly SemaphoreSlim _flushGate = new(1);
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (!settings.UploadCrashReports || mode.SuppressNetwork) return;
        await _flushGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var files = queue.GetPendingFiles();
            if (files.Length == 0 || await licenses.GetCrashAuthenticationAsync(cancellationToken).ConfigureAwait(false) is not { } authentication) return;
            using var http = clients.Create();
            foreach (var path in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(path) || new FileInfo(path).Length > CrashReportQueue.MaximumReportBytes) continue;
                CrashReport? report;
                try { report = JsonSerializer.Deserialize<CrashReport>(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false), CrashReportQueue.JsonOptions); }
                catch (JsonException) { continue; }
                if (report is null || report.Application != "WawiCallPro" || report.ReportId == Guid.Empty) continue;
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/crash");
                request.Content = JsonContent.Create(report, options: CrashReportQueue.JsonOptions);
                request.Headers.Add("X-LicenseId", authentication.LicenseId.ToString("D"));
                request.Headers.Add("X-MachineIdHash", authentication.MachineIdHash);
                request.Headers.Add("X-ActivationSecret", authentication.ActivationSecret);
                using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return;
                File.Delete(path);
            }
        }
        finally { _flushGate.Release(); }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.UploadCrashReports || mode.SuppressNetwork) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.CrashFlushIntervalSeconds));
        do
        {
            try { await FlushAsync(stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch { /* Preserve the queue for the next bounded attempt; upload failure is not a process crash. */ }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
