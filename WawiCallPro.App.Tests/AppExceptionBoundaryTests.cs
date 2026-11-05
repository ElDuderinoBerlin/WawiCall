using Microsoft.Extensions.Logging.Abstractions;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppExceptionBoundaryTests
{
    [TestMethod]
    public void EarlyFatalIsCapturedOnceAndForwardedWhenHostBecomesAvailable()
    {
        var reports = new ProbeReporter(); using var boundary = new AppExceptionBoundary(reports);
        var first = new InvalidOperationException("private-early-error");
        boundary.Report(first, CrashOrigin.Startup);
        boundary.Report(new Exception(), CrashOrigin.Process);
        var state = new RuntimeFailureState();
        boundary.Attach(state, NullLogger.Instance);
        Assert.AreSame(first, state.Failure);
        Assert.AreEqual(1, reports.Count);
    }

    [TestMethod]
    public void CancellationOnlyIsIgnoredButMixedAggregateStillFails()
    {
        var reports = new ProbeReporter(); using var boundary = new AppExceptionBoundary(reports);
        boundary.Report(new AggregateException(new TaskCanceledException(), new OperationCanceledException()), CrashOrigin.BackgroundTask);
        Assert.IsNull(boundary.Failure);
        boundary.Report(new AggregateException(new OperationCanceledException(), new IOException()), CrashOrigin.BackgroundTask);
        Assert.IsNotNull(boundary.Failure);
        Assert.AreEqual(1, reports.Count);
    }

    private sealed class ProbeReporter : ICrashReporter
    {
        public int Count { get; private set; }
        public CrashReceipt Capture(Exception exception, CrashOrigin origin) { Count++; return new(Guid.NewGuid(), true); }
    }
}
