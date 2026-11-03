using Microsoft.Extensions.Hosting;

namespace WawiCallPro.App.Hosting;

/// <summary>The WinForms message loop owns process lifetime; no console signal/ProcessExit handlers are installed.</summary>
internal sealed class AppHostLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
