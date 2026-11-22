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
using WawiCallPro.Automation;

namespace WawiCallPro.App.Hosting;

/// <summary>Composition-root orchestration; the message loop stays alive until host shutdown has completed.</summary>
internal sealed class AppApplicationContext : ApplicationContext
{
    private readonly IHost _host;
    private readonly bool _diagnostics;
    private readonly string? _auditDirectory;
    private readonly TaskCompletionSource _closeRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private MainForm? _form;
    private bool _windowReady;
    private readonly AppExceptionBoundary _exceptions;
    private readonly IAppInstanceGuard _instance;
    private readonly IUiDispatcher _dispatcher;
    private readonly CancellationTokenSource _activationStopping = new();
    private readonly CancellationToken _activationToken;
    private readonly StartupSplash? _splash;
    private readonly AppAutomationState _automation;
    private readonly StartupExecutionMode _mode;
    private readonly AutomationOperations _operations;
    private AutomationClassRegistration? _registration;
    private readonly System.Windows.Forms.Timer _idleTimer = new() { Interval = 1000 };
    private readonly string? _fixtureDirectory;

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
        _automation = host.Services.GetRequiredService<AppAutomationState>();
        _mode = host.Services.GetRequiredService<StartupExecutionMode>();
        _operations = host.Services.GetRequiredService<AutomationOperations>();
        _automation.ShowRequested = ShowAutomationAsync;
        _automation.PresentRequested = PresentAutomationAsync;
        if (!_mode.ComActivated) _automation.EnterUserMode();
        var identity = host.Services.GetRequiredService<AppComIdentity>();
        _fixtureDirectory = identity.FixtureDirectory;
        if (!_mode.SuppressNetwork && identity.Enabled)
        {
            _registration = new(identity.ClassId, host.Services.GetRequiredService<IAutomationApplicationFactory>(), _automation.ServerLock);
            if (_instance is IComInstancePublication publication) publication.PublishAutomation();
            _idleTimer.Tick += CheckIdle;
            _idleTimer.Start();
        }
        _instance.ActivationRequested += OnActivationRequested;
        Application.Idle += StartOnIdle;
    }

    internal Exception? Failure { get; private set; }
    internal bool RestartRequested { get; private set; }

    private async void StartOnIdle(object? sender, EventArgs e)
    {
        Application.Idle -= StartOnIdle;
        try
        {
            HostDiagnosticSnapshot? diagnostic = null;
            await HostExecution.RunAsync(_host, async stoppingToken =>
            {
                if (_mode.ComActivated)
                {
                    _windowReady = true;
                    OnActivationRequested(this, EventArgs.Empty);
                    using var stopping = stoppingToken.Register(() => _closeRequested.TrySetResult());
                    await _closeRequested.Task;
                    await CompleteAutomationAsync();
                    return;
                }
                if (_auditDirectory is not null)
                {
                    if (NativeTrayAudit.IsRequested)
                    {
                        if (!_instance.TryAcquire()) throw new InvalidOperationException("Native audit requires an isolated instance profile.");
                        _form = CreateMainForm();
                        _form.FormClosing += MainFormClosing;
                        await new NativeTrayAudit(_auditDirectory).RunAsync(_form, () =>
                        {
                            _windowReady = true;
                            OnActivationRequested(this, EventArgs.Empty);
                        }, stoppingToken);
                        return;
                    }
                    UiAudit.Render(_auditDirectory, CreateMainForm);
                    return;
                }

                // The context owns the form so DI can never dispose a WinForms control on a worker thread.
                if (_splash is not null) await _splash.UpdateDetailedAsync("Hauptfenster wird vorbereitet …", 95, cancellationToken: stoppingToken);
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
                    await _splash.UpdateDetailedAsync("WawiCallPro ist bereit.", 100, cancellationToken: stoppingToken);
                    await _splash.CompleteAsync(_form);
                }
                _windowReady = true;
                _automation.Ready();
                _automation.Visible();
                _automation.Telephony = _host.Services.GetRequiredService<WawiCallPro.Interfaces.Telephony.ITelephonyService>();
                OnActivationRequested(this, EventArgs.Empty);
                if (_diagnostics)
                {
                    var diagnostics = _host.Services.GetRequiredService<HostDiagnostics>();
                    diagnostic = await Task.Run(() => diagnostics.CaptureAsync(stoppingToken), stoppingToken);
                    _form.Close();
                }
                await _closeRequested.Task;
                await CompleteAutomationAsync();
            });
            // Emit success only after StopAsync and asynchronous container disposal have succeeded.
            if (_diagnostics) Console.WriteLine(JsonSerializer.Serialize(diagnostic));
            RestartRequested = !_diagnostics && _auditDirectory is null && _form?.RestartRequested == true;
        }
        catch (Exception exception)
        {
            _exceptions.Report(exception, CrashOrigin.Host);
            Failure = exception;
        }
        finally
        {
            RevokeAutomation();
            _idleTimer.Dispose();
            _automation.ShowRequested = null;
            _automation.PresentRequested = null;
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
            finally
            {
                try
                {
                    if (_fixtureDirectory is { } directory)
                        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "com-stopped.json"),
                            JsonSerializer.Serialize(new { Success = Failure is null, Revoked = _registration is null }));
                }
                finally { _activationStopping.Dispose(); ExitThread(); }
            }
        }
    }

    private MainForm CreateMainForm()
    {
        var form = ActivatorUtilities.CreateInstance<MainForm>(_host.Services);
        form.VisibleChanged += OnMainFormVisibilityChanged;
        if (_host.Services.GetService<IAppHotkeyFactory>() is { } hotkeys)
            form.ConfigureHotkeys(hotkeys, _host.Services.GetRequiredService<ApplicationConfigurationService>().Current.Options.User);
        return form;
    }

    private void OnMainFormVisibilityChanged(object? sender, EventArgs args)
    {
        if (sender is MainForm form) _automation.UpdateVisibility(form.Visible);
    }

    private void RevokeAutomation()
    {
        _automation.Stopping();
        _idleTimer.Stop();
        if (_instance is IComInstancePublication publication) publication.WithdrawAutomation();
        _registration?.Dispose();
        _registration = null;
    }

    private async Task CompleteAutomationAsync()
    {
        RevokeAutomation();
        if (_fixtureDirectory is { } directory)
        {
            var delayFile = System.IO.Path.Combine(directory, "shutdown-delay.request");
            if (System.IO.File.Exists(delayFile))
            {
                var delay = int.Parse(System.IO.File.ReadAllText(delayFile), System.Globalization.CultureInfo.InvariantCulture);
                System.IO.File.Delete(delayFile);
                if (delay is < 0 or > 5000) throw new ArgumentOutOfRangeException(nameof(delay));
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "com-stopping.json"), JsonSerializer.Serialize(new { Pid = Environment.ProcessId }));
                await Task.Delay(delay);
            }
        }
        await _operations.DisposeAsync();
    }

    private void CheckIdle(object? sender, EventArgs args)
    {
        if (_fixtureDirectory is { } directory)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "com-runtime.json"), JsonSerializer.Serialize(new
            {
                Pid = Environment.ProcessId, Forms = Application.OpenForms.Count, FormCreated = _form is not null,
                WindowHandle = _form is { IsHandleCreated: true } ? _form.Handle.ToInt64() : 0,
                Views = _form?.AutomationDiagnostics(),
                Visible = _form?.Visible == true, State = _automation.GetStatus().State, SipState = _automation.GetStatus().SipState,
                ServerLocks = _automation.ServerLocks
            }));
            var stop = System.IO.Path.Combine(directory, "stop.request");
            var closeDialogs = System.IO.Path.Combine(directory, "close-dialog.request");
            if (_form is not null && System.IO.File.Exists(closeDialogs))
            {
                System.IO.File.Delete(closeDialogs);
                foreach (var dialog in _form.OwnedForms.ToArray()) dialog.Close();
            }
            if (System.IO.File.Exists(stop))
            {
                System.IO.File.Delete(stop);
                if (_form is not null) _form.Close(); else _closeRequested.TrySetResult();
            }
            var launch = System.IO.Path.Combine(directory, "launch.request");
            if (_form is not null && System.IO.File.Exists(launch))
            {
                var action = System.IO.File.ReadAllText(launch).Trim();
                System.IO.File.Delete(launch);
                if (action == "maximized") _form.WindowState = FormWindowState.Maximized;
                if (action is "minimized" or "maximized") _form.WindowState = FormWindowState.Minimized;
                if (action == "hidden") _form.MoveToTray();
                // Fixture only: match the existing native tray audit's Explorer launch from an explicitly focused owner.
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
                try { shell.ShellExecute(Environment.ProcessPath!, "--com-fixture \"" + directory + "\"", AppContext.BaseDirectory, "open", 1); }
                finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
            }
        }
        if (_automation.TryIdleShutdown(_operations.HasPendingOperations)) _closeRequested.TrySetResult();
    }

    private async Task ShowAutomationAsync(CancellationToken token)
    {
        if (!_windowReady) throw new AutomationFault(AutomationFailure.NotReady);
        _automation.EnterUserMode();
        if (_mode.ComActivated) await _host.Services.GetRequiredService<AppStartupPipeline>().ResumeAsync(token);
        _automation.Telephony = _host.Services.GetRequiredService<WawiCallPro.Interfaces.Telephony.ITelephonyService>();
        token.ThrowIfCancellationRequested();
        if (_form is null)
        {
            _form = CreateMainForm();
            _form.FormClosing += MainFormClosing;
            _form.ShowAfterStartup(false);
        }
        else _form.RestoreFromTray();
        _automation.Visible();
    }

    private async Task ActivateRequestedAsync()
    {
        if (!_windowReady || !_instance.TryConsumeActivation()) return;
        await ShowAutomationAsync(_activationToken);
    }

    private async Task PresentAutomationAsync(PresentationRequest request, CancellationToken token)
    {
        await ShowAutomationAsync(token);
        if (_form is null) throw new AutomationFault(AutomationFailure.NotReady);
        await _form.PresentAutomationAsync(request, token);
        await _host.Services.GetRequiredService<AppAutomationCatalog>().RefreshAsync(token);
    }

    private async void OnActivationRequested(object? sender, EventArgs args)
    {
        try
        {
            Task work = Task.CompletedTask;
            await _dispatcher.InvokeAsync(() =>
            {
                work = ActivateRequestedAsync();
            }, _activationToken);
            await work;
        }
        catch (OperationCanceledException) when (_activationToken.IsCancellationRequested) { }
        catch (Exception exception) { _exceptions.Report(exception, CrashOrigin.UiThread); }
    }

    private async void MainFormClosing(object? sender, FormClosingEventArgs e)
    {
        e.Cancel = true;
        try
        {
            if (_form is not null)
            {
                if (!_host.Services.GetRequiredService<ClientKeyExitProtection>().TryComplete(_form)) return;
                await _form.PrepareToCloseAsync();
            }
        }
        catch (Exception exception) when (exception is WawiCallPro.Plugin.Windows.UnsavedPluginChangesException or UnsavedCallNotesException)
        { if (_form is not null) { _form.RestartRequested = false; _form.ReportUnsavedChanges(); } return; }
        catch (ActiveCallCloseException)
        { if (_form is not null) _form.ReportActiveCall(); return; }
        catch (Exception exception) { _exceptions.Report(exception, CrashOrigin.UiThread); }
        _closeRequested.TrySetResult();
    }
}
