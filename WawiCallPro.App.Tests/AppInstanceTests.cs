using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppInstanceTests
{
    [TestMethod]
    public void ComSuccessorWaitsForRevokedOwnerAndTakesOverWithoutAnActivationSignal()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WawiCallPro-com-handoff-" + Guid.NewGuid().ToString("N"));
        using var owner = Create(directory, "Pro"); Assert.IsTrue(owner.TryAcquire()); owner.PublishAutomation();
        var joined = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var proceed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                using var next = Create(directory, "Pro"); Assert.IsFalse(next.TryAcquire());
                Assert.IsTrue(next.WaitForAutomation(TimeSpan.Zero)); joined.TrySetResult();
                if (!proceed.Wait(TimeSpan.FromSeconds(4))) throw new TimeoutException();
                Assert.IsFalse(next.WaitForAutomation(TimeSpan.FromSeconds(4)));
                Assert.IsTrue(next.TryAcquire()); next.PublishAutomation();
                Assert.IsFalse(next.TryConsumeActivation()); finished.TrySetResult();
            }
            catch (Exception error) { joined.TrySetException(error); finished.TrySetException(error); }
        }) { IsBackground = true };
        thread.Start(); joined.Task.WaitAsync(TimeSpan.FromSeconds(4)).GetAwaiter().GetResult();
        owner.WithdrawAutomation(); proceed.Set();
        Assert.IsFalse(finished.Task.IsCompleted);
        owner.Dispose();
        finished.Task.WaitAsync(TimeSpan.FromSeconds(4)).GetAwaiter().GetResult();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2))); Assert.IsFalse(Directory.Exists(directory));
    }
    [TestMethod]
    public void ComReadinessWaitDoesNotRequestWindowActivation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WawiCallPro-com-ready-" + Guid.NewGuid().ToString("N"));
        using var owner = Create(directory, "Pro"); Assert.IsTrue(owner.TryAcquire());
        RunOnOtherThread(() =>
        {
            using var second = Create(directory, "Pro"); Assert.IsFalse(second.TryAcquire());
            Assert.IsFalse(second.WaitForAutomation(TimeSpan.Zero));
            owner.PublishAutomation();
            Assert.IsTrue(second.WaitForAutomation(TimeSpan.FromSeconds(1)));
        });
        Assert.IsFalse(owner.TryConsumeActivation());
    }
    [TestMethod]
    public void ActivationOnlyRequiresAnExistingOwnerAndNeverCreatesAProfile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WawiCallPro-activation-only-" + Guid.NewGuid().ToString("N"));
        Assert.AreEqual(2, Program.ActivateExisting(["--activate-existing", "relative-profile"]));
        Assert.AreEqual(2, Program.ActivateExisting(["--activate-existing", directory]));
        var descriptor = ApplicationDescriptor.FromAssembly(typeof(Program).Assembly);
        using var owner = Create(directory, descriptor.ProductLine);
        Assert.IsTrue(owner.TryAcquire());
        RunOnOtherThread(() => Assert.AreEqual(0, Program.ActivateExisting(["--activate-existing", directory])));
        Assert.IsTrue(SpinWait.SpinUntil(owner.TryConsumeActivation, TimeSpan.FromSeconds(5)));
        Assert.IsFalse(Directory.Exists(directory));
    }

    [TestMethod]
    public void WindowsMutexExcludesSecondOwnerAndActivationSurvivesBeforeSubscription()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WawiCallPro-instance-" + Guid.NewGuid().ToString("N"));
        using var first = Create(directory, "Pro");
        Assert.IsTrue(first.TryAcquire());
        RunOnOtherThread(() =>
        {
            using var second = Create(directory, "Pro");
            Assert.IsFalse(second.TryAcquire());
            second.RequestExistingInstanceActivation();
        });
        Assert.IsTrue(SpinWait.SpinUntil(first.TryConsumeActivation, TimeSpan.FromSeconds(5)));
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        first.ActivationRequested += (_, _) => received.TrySetResult();
        // Mutex ownership belongs to an OS thread; use a distinct owner and keep disposal on its thread.
        RunOnOtherThread(() =>
        {
            using var second = Create(directory, "Pro");
            Assert.IsFalse(second.TryAcquire());
            second.RequestExistingInstanceActivation();
        });
        received.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Assert.IsTrue(SpinWait.SpinUntil(first.TryConsumeActivation, TimeSpan.FromSeconds(5)));
        Assert.IsFalse(first.TryConsumeActivation());
        Assert.IsFalse(Directory.Exists(directory));
    }

    [TestMethod]
    public void ProductLinesAndExplicitProfilesHaveIndependentOwners()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WawiCallPro-instance-" + Guid.NewGuid().ToString("N"));
        using var pro = Create(directory, "Pro");
        using var finOffice = Create(directory, "FinOffice");
        using var otherProfile = Create(directory + "-other", "Pro");
        Assert.IsTrue(pro.TryAcquire());
        Assert.IsTrue(finOffice.TryAcquire());
        Assert.IsTrue(otherProfile.TryAcquire());
    }

    private static AppInstanceGuard Create(string root, string line) => new(new ApplicationDescriptor("WawiCallPro", line),
        new ApplicationPaths(new ConfigurationLocation(root)));

    private static void RunOnOtherThread(Action operation)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { operation(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        }) { IsBackground = true };
        thread.Start();
        completion.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2)));
    }
}
