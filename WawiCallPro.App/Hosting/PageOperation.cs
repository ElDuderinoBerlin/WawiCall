using DevExpress.XtraEditors;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.App.Hosting;

/// <summary>Tracks one page action so shutdown can cancel and await work before its controls are disposed.</summary>
internal sealed class PageOperation(Control owner, LabelControl status) : IDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private Task _running = Task.CompletedTask;
    private bool _disposed;
    internal bool IsRunning => !_running.IsCompleted;

    internal Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (_disposed || IsRunning || _stopping.IsCancellationRequested) return _running;
        _running = RunCoreAsync(operation);
        return _running;
    }

    private async Task RunCoreAsync(Func<CancellationToken, Task> operation)
    {
        status.Text = "Bitte warten …";
        try { await operation(_stopping.Token); }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!owner.IsDisposed) status.Text = Describe(exception);
        }
    }

    internal async Task StopAsync()
    {
        await _stopping.CancelAsync();
        await _running;
    }

    internal static string Describe(Exception exception) => exception switch
    {
        ConfigurationException or DatabaseOperationException or RepositoryConflictException or SecretProtectionException => exception.Message,
        NotSupportedException => "Diese Funktion ist im aktiven Plugin noch nicht verfügbar.",
        _ => "Die Aktion ist fehlgeschlagen. Bitte Einstellungen und Erreichbarkeit prüfen."
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stopping.Cancel();
        _stopping.Dispose();
    }
}
