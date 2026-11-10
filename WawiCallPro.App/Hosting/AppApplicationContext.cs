using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;
using Microsoft.Extensions.Logging;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Hosting;

/// <summary>Composition-root orchestration; the message loop stays alive until host shutdown has completed.</summary>
internal sealed class AppApplicationContext : ApplicationContext
{
    private readonly IHost _host;
    private readonly bool _diagnostics;
    private readonly string? _auditDirectory;
    private readonly TaskCompletionSource _closeRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private MainForm? _form;
    private readonly AppExceptionBoundary _exceptions;
    private readonly IAppInstanceGuard _instance;
    private readonly IUiDispatcher _dispatcher;
    private readonly CancellationTokenSource _activationStopping = new();
    private readonly CancellationToken _activationToken;
    private readonly StartupSplash? _splash;

    internal AppApplicationContext(IHost host, bool diagnostics, string? auditDirectory)
    {
        _host = host;
        _diagnostics = diagnostics;
        _auditDirectory = auditDirectory;
        _exceptions = host.Services.GetRequiredService<AppExceptionBoundary>();
        _exceptions.Attach(host.Services.GetRequiredService<RuntimeFailureState>(),
            host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WawiCallPro.App"));
        _instance = host.Services.GetRequiredService<IAppInstanceGuard>();
        _activationToken = _activationStopping.Token;
        _dispatcher = host.Services.GetRequiredService<IUiDispatcher>();
        _splash = host.Services.GetService<StartupSplash>();
        _instance.ActivationRequested += OnActivationRequested;
        Application.Idle += StartOnIdle;
    }

    internal Exception? Failure { get; private set; }

    private async void StartOnIdle(object? sender, EventArgs e)
    {
        Application.Idle -= StartOnIdle;
        try
        {
            HostDiagnosticSnapshot? diagnostic = null;
            await HostExecution.RunAsync(_host, async stoppingToken =>
            {
                if (_auditDirectory is not null)
                {
                    UiAudit.Render(_auditDirectory, CreateMainForm);
                    return;
                }

                // The context owns the form so DI can never dispose a WinForms control on a worker thread.
                if (_splash is not null) await _splash.UpdateAsync("Hauptfenster wird vorbereitet …", 4, stoppingToken);
                _form = CreateMainForm();
                _form.FormClosing += MainFormClosing;
                if (_diagnostics || _host.Services.GetRequiredService<AppWindowOptions>().Hidden)
                {
                    _form.PrepareForBackgroundRun();
                }
                else if (_host.Services.GetRequiredService<ApplicationConfigurationService>().Current.Options.User.StartMinimized)
                    _form.WindowState = FormWindowState.Minimized;
                using var stopRegistration = stoppingToken.Register(() => _closeRequested.TrySetResult());
                _form.ShowAfterStartup(_splash?.ShouldPreserveForeground() == true);
                if (_splash is not null)
                {
                    await _splash.UpdateAsync("WawiCallPro ist bereit.", 5, stoppingToken);
                    await _splash.CompleteAsync(_form);
                }
                OnActivationRequested(this, EventArgs.Empty);
                if (_diagnostics)
                {
                    var diagnostics = _host.Services.GetRequiredService<HostDiagnostics>();
                    diagnostic = await Task.Run(() => diagnostics.CaptureAsync(stoppingToken), stoppingToken);
                    _form.Close();
                }
                await _closeRequested.Task;
            });
            // Emit success only after StopAsync and asynchronous container disposal have succeeded.
            if (_diagnostics) Console.WriteLine(JsonSerializer.Serialize(diagnostic));
        }
        catch (Exception exception)
        {
            _exceptions.Report(exception, CrashOrigin.Host);
            Failure = exception;
        }
        finally
        {
            _instance.ActivationRequested -= OnActivationRequested;
            await _activationStopping.CancelAsync();
            try
            {
                try { if (_splash is not null) await _splash.CloseAsync(); }
                finally
                {
                    if (_form is not null)
                    {
                        _form.FormClosing -= MainFormClosing;
                        try { _form.Close(); }
                        finally { _form.Dispose(); }
                    }
                }
            }
            catch (Exception exception)
            {
                Failure = Failure is null ? exception : new AggregateException(Failure, exception);
            }
            finally { _activationStopping.Dispose(); ExitThread(); }
        }
    }

    private MainForm CreateMainForm() => ActivatorUtilities.CreateInstance<MainForm>(_host.Services);

    private async void OnActivationRequested(object? sender, EventArgs args)
    {
        try
        {
            await _dispatcher.InvokeAsync(() =>
            {
                if (_form is not { IsDisposed: false, Visible: true } || !_instance.TryConsumeActivation()) return;
                if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
                _form.Activate();
            }, _activationToken);
        }
        catch (OperationCanceledException) when (_activationToken.IsCancellationRequested) { }
        catch (Exception exception) { _exceptions.Report(exception, CrashOrigin.UiThread); }
    }

    private async void MainFormClosing(object? sender, FormClosingEventArgs e)
    {
        e.Cancel = true;
        try
        {
            if (_form is not null) await _form.PrepareToCloseAsync();
        }
        catch (WawiCallPro.Plugin.Windows.UnsavedPluginChangesException) { _form?.ReportUnsavedChanges(); return; }
        catch (Exception exception) { _exceptions.Report(exception, CrashOrigin.UiThread); }
        _closeRequested.TrySetResult();
    }
}
