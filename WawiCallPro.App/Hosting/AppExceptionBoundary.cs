using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Operations;
using Microsoft.Extensions.Logging;

namespace WawiCallPro.App.Hosting;

/// <summary>Installed before UI/host construction; synchronous persistence also works during process termination.</summary>
internal sealed class AppExceptionBoundary(ICrashReporter reports) : IDisposable
{
    private readonly object _gate = new();
    private RuntimeFailureState? _runtime;
    private bool _installed;
    private ILogger? _logger;
    internal Exception? Failure { get; private set; }
    internal CrashReceipt? Receipt { get; private set; }

    internal void Install()
    {
        if (_installed) return;
        Application.ThreadException += OnUiFailure;
        TaskScheduler.UnobservedTaskException += OnTaskFailure;
        AppDomain.CurrentDomain.UnhandledException += OnProcessFailure;
        _installed = true;
    }

    internal void Attach(RuntimeFailureState runtime, ILogger logger)
    {
        lock (_gate)
        {
            _runtime = runtime;
            _logger = logger;
            if (Failure is { } failure) runtime.Report(failure);
        }
    }

    internal void Report(Exception exception, CrashOrigin origin)
    {
        if (IsCancellationOnly(exception)) return;
        lock (_gate)
        {
            if (Failure is not null) return;
            Failure = exception;
            Receipt = reports.Capture(exception, origin);
            if (_logger is not null)
                OperationEvents.Write(_logger, origin == CrashOrigin.UiThread ? OperationEvent.UiFailed : OperationEvent.BackgroundFailed, exception);
            _runtime?.Report(exception);
        }
    }

    private void OnUiFailure(object sender, ThreadExceptionEventArgs args) => Report(args.Exception, CrashOrigin.UiThread);
    private void OnTaskFailure(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        Report(args.Exception, CrashOrigin.BackgroundTask);
        args.SetObserved();
    }
    private void OnProcessFailure(object sender, UnhandledExceptionEventArgs args) =>
        Report(args.ExceptionObject as Exception ?? new InvalidOperationException("Non-exception process failure."), CrashOrigin.Process);

    internal static bool IsCancellationOnly(Exception exception) => exception is OperationCanceledException
        || exception is AggregateException aggregate && aggregate.InnerExceptions.Count > 0 && aggregate.InnerExceptions.All(IsCancellationOnly);

    public void Dispose()
    {
        if (!_installed) return;
        Application.ThreadException -= OnUiFailure;
        TaskScheduler.UnobservedTaskException -= OnTaskFailure;
        AppDomain.CurrentDomain.UnhandledException -= OnProcessFailure;
        lock (_gate) _runtime = null;
        _installed = false;
    }
}
