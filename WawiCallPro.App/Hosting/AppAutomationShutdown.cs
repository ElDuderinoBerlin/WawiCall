using Microsoft.Extensions.Hosting;
using WawiCallPro.Automation;

namespace WawiCallPro.App.Hosting;

/// <summary>Stops accepting requests and drains the shared worker before phone, plugin or persistence services stop.</summary>
internal sealed class AppAutomationShutdown(AppAutomationState state, AutomationOperations operations) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        state.Stopping();
        // A timeout requests cancellation; it does not permit disposing dependencies still used by a command.
        await operations.DisposeAsync();
    }
}
