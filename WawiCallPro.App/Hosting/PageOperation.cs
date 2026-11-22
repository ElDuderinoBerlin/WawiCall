using DevExpress.XtraEditors;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.App.Hosting;

/// <summary>Tracks one page action so shutdown can cancel and await work before its controls are disposed.</summary>
internal sealed class PageOperation : IDisposable
{
    private readonly Control owner;
    private readonly LabelControl? status;
    private readonly Action<WawiCallPro.Utilities.Operations.OperationEvent>? report;
    internal PageOperation(Control owner, LabelControl status) { this.owner = owner; this.status = status; }
    internal PageOperation(Control owner, Action<WawiCallPro.Utilities.Operations.OperationEvent> report) { this.owner = owner; this.report = report; }
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
        using var waiting = new UiWaitCursor(owner);
        if (report is not null) report(WawiCallPro.Utilities.Operations.OperationEvent.SettingsWaiting);
        else status!.Text = "Bitte warten …";
        try { await operation(_stopping.Token); }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!owner.IsDisposed)
            {
                if (report is not null) report(exception is ConfigurationException or WawiCallPro.Interfaces.Plugins.PluginSettingsValidationException
                    ? WawiCallPro.Utilities.Operations.OperationEvent.SettingsValidationFailed : WawiCallPro.Utilities.Operations.OperationEvent.SettingsFailed);
                else status!.Text = Describe(exception);
            }
        }
    }

    internal async Task StopAsync()
    {
        if (!_disposed) await _stopping.CancelAsync();
        await _running;
    }

    internal static string Describe(Exception exception) => exception switch
    {
        WawiCallPro.Interfaces.Plugins.PluginSettingsValidationException => exception.Message,
        WawiCallPro.Interfaces.Office.MailException => exception.Message,
        WawiCallPro.Interfaces.Telephony.TelephonyException or ConfigurationException or DatabaseOperationException or RepositoryConflictException or SecretProtectionException or WawiCallPro.Interfaces.PhoneNumbers.PhoneCacheRefreshException => exception.Message,
        WawiCallPro.Plugin.Windows.UnsavedPluginChangesException => exception.Message,
        UnsavedCallNotesException => exception.Message,
        ActiveCallCloseException => exception.Message,
        AutoStartException => exception.Message,
        WawiCallPro.Utilities.Plugins.LexwareUnavailableException => exception.Message,
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
