using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppInstanceTests
{
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
