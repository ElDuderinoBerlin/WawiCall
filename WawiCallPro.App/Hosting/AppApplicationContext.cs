using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App.Hosting;

/// <summary>Composition-root orchestration; the message loop stays alive until host shutdown has completed.</summary>
internal sealed class AppApplicationContext : ApplicationContext
{
    private readonly IHost _host;
    private readonly bool _diagnostics;
    private readonly string? _auditDirectory;
    private readonly TaskCompletionSource _closeRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private MainForm? _form;

    internal AppApplicationContext(IHost host, bool diagnostics, string? auditDirectory)
    {
        _host = host;
        _diagnostics = diagnostics;
        _auditDirectory = auditDirectory;
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
                _form = CreateMainForm();
                _form.FormClosing += MainFormClosing;
                if (_diagnostics)
                {
                    _form.ShowInTaskbar = false;
                    _form.Opacity = 0;
                }
                using var stopRegistration = stoppingToken.Register(() => _closeRequested.TrySetResult());
                _form.Show();
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
            Failure = exception;
        }
        finally
        {
            if (_form is not null)
            {
                _form.FormClosing -= MainFormClosing;
                _form.Close();
                _form.Dispose();
            }
            ExitThread();
        }
    }

    private MainForm CreateMainForm() => ActivatorUtilities.CreateInstance<MainForm>(_host.Services);

    private void MainFormClosing(object? sender, FormClosingEventArgs e)
    {
        e.Cancel = true;
        _closeRequested.TrySetResult();
    }
}
