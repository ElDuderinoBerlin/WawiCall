using DevExpress.LookAndFeel;
using DevExpress.XtraEditors;
using Microsoft.Extensions.Hosting;
using Quantarix.Windows.Win32;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Hosting;

/// <summary>Independent STA message loop keeps the LxTranslate-style splash animated during host startup.</summary>
internal sealed class StartupSplash : IAsyncDisposable
{
    private readonly TaskCompletionSource<FrmStartupSplashScreen> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Thread? _thread;
    private nint _handle;
    private int _closeRequested;

    internal nint WindowHandle => Volatile.Read(ref _handle);

    internal bool ShouldPreserveForeground()
    {
        var handle = WindowHandle;
        var foreground = User32.GetForegroundWindow();
        return handle != 0 && foreground != 0 && foreground != handle;
    }

    // Called on the main UI thread so the appearance snapshot precedes the new message loop.
    internal async Task ShowAsync(CancellationToken cancellationToken, bool hidden = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_thread is not null) throw new InvalidOperationException("Startup splash has already been started.");
        var skin = UserLookAndFeel.Default.ActiveSkinName;
        var palette = UserLookAndFeel.Default.ActiveSvgPaletteName;
        var font = WindowsFormsSettings.DefaultFont;
        _thread = new Thread(() => RunMessageLoop(skin, palette, font, hidden))
        {
            IsBackground = true,
            Name = "WawiCallPro startup splash"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        try { await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken); }
        catch { await CloseAsync(); throw; }
    }

    private void RunMessageLoop(string skin, string? palette, Font font, bool hidden)
    {
        try
        {
            using var synchronization = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synchronization);
            using var form = new FrmStartupSplashScreen();
            form.LookAndFeel.UseDefaultLookAndFeel = false;
            if (string.IsNullOrWhiteSpace(palette)) form.LookAndFeel.SetSkinStyle(skin);
            else form.LookAndFeel.SetSkinStyle(skin, palette);
            form.Font = font;
            if (hidden) form.PrepareForBackgroundRun();
            form.SetStatus("Lizenz und Anwendungsdienste werden vorbereitet …", 1, 5);
            form.Shown += (_, _) =>
            {
                // Never access this control's Handle from the main thread.
                Volatile.Write(ref _handle, form.Handle);
                _ready.TrySetResult(form);
                if (Volatile.Read(ref _closeRequested) != 0) form.Close();
            };
            Application.Run(form);
        }
        catch (Exception exception) { _ready.TrySetException(exception); }
        finally
        {
            Volatile.Write(ref _handle, 0);
            _stopped.TrySetResult();
        }
    }

    internal async Task UpdateAsync(string text, int step, CancellationToken cancellationToken = default)
    {
        if (_thread is null || Volatile.Read(ref _closeRequested) != 0) return;
        var form = await _ready.Task.WaitAsync(cancellationToken);
        if (!form.IsDisposed)
            await form.InvokeAsync(() => form.SetStatus(text, step, 5), cancellationToken);
    }

    internal Task CompleteAsync(Form mainForm) => StartupFocusHandoff.CompleteAsync(
        () =>
        {
            var foreground = User32.GetForegroundWindow();
            return foreground != 0 && (foreground == WindowHandle ||
                mainForm.IsHandleCreated && foreground == mainForm.Handle);
        }, CloseAsync,
        () =>
        {
            if (!mainForm.IsDisposed && mainForm.Visible && mainForm.WindowState != FormWindowState.Minimized)
                mainForm.Activate();
        });

    internal async Task CloseAsync()
    {
        Interlocked.Exchange(ref _closeRequested, 1);
        if (_thread is null) return;
        try
        {
            var form = await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (!form.IsDisposed) await form.InvokeAsync(form.Close, CancellationToken.None);
        }
        catch (InvalidOperationException) when (_stopped.Task.IsCompleted) { }
        finally
        {
            await _stopped.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (!_thread.Join(TimeSpan.FromSeconds(2))) throw new TimeoutException("Startup splash did not stop.");
        }
    }

    public ValueTask DisposeAsync() => new(CloseAsync());
}

/// <summary>Runs after DevExpress initialization and before licensing; diagnostic and tray starts stay hidden.</summary>
internal sealed class StartupSplashHost(StartupSplash splash, AppUiState state, IUiDispatcher dispatcher,
    StartupExecutionMode mode, AppWindowOptions window, ApplicationConfigurationService configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Task showing = Task.CompletedTask;
        await dispatcher.InvokeAsync(() =>
        {
            state.RestoreAppearance();
            if (!mode.SuppressNetwork && !window.Hidden && !configuration.Current.Options.User.StartMinimized)
                showing = splash.ShowAsync(cancellationToken);
        }, cancellationToken);
        await showing;
    }

    public Task StopAsync(CancellationToken cancellationToken) => splash.CloseAsync();
}
