using Microsoft.Extensions.Hosting;

namespace WawiCallPro.Utilities.Operations;

/// <summary>Turns fatal worker/UI/logging failures into the standard application's stop signal.</summary>
internal sealed class RuntimeFailureMonitor(RuntimeFailureState failures, IHostApplicationLifetime lifetime) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        failures.Failed += OnFailed;
        if (failures.Failure is { } failure) throw failure;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        failures.Failed -= OnFailed;
        return Task.CompletedTask;
    }

    private void OnFailed(object? sender, EventArgs e) => lifetime.StopApplication();
}
