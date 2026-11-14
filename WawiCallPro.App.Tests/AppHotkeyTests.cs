using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

[TestClass, DoNotParallelize]
public sealed class AppHotkeyTests
{
    [TestMethod]
    public void GlobalHotkeySessionValidatesMessagesIgnoresStaleIdsAndReleasesOnlyOwnedRegistrations()
    {
        var backend = new HotkeyBackendDouble(); var actions = new List<AppHotkeyAction>();
        var factory = new AppHotkeyFactory(backend, new(false), NullLogger<AppHotkeyFactory>.Instance);
        var settings = new UserSettings { ShowWindowHotkey = "Ctrl+Alt+F3", DialHotkey = "Ctrl+Alt+F4" };
        using var session = factory.Create(new(42), settings, actions.Add);
        var show = backend.Registered[0]; var dial = backend.Registered[1];
        Assert.AreEqual(0x4003, show.Modifiers); Assert.AreEqual(0x72, show.Key);
        Assert.IsFalse(session.TryDispatch(new(show.Id), MessageData(show.Key, 0)));
        Assert.IsFalse(session.TryDispatch(new(show.Id), MessageData(dial.Key)));
        Assert.IsFalse(session.TryDispatch(new(9999), MessageData(show.Key)));
        Assert.IsFalse(session.TryDispatch(new(long.MaxValue), MessageData(show.Key)));
        Assert.IsFalse(session.TryDispatch(new(show.Id), new(long.MaxValue)));
        Assert.IsTrue(session.TryDispatch(new(show.Id), MessageData(show.Key)));
        Assert.IsTrue(session.TryDispatch(new(dial.Id), MessageData(dial.Key)));
        CollectionAssert.AreEqual(new[] { AppHotkeyAction.ShowWindow, AppHotkeyAction.OpenDial }, actions);
        session.Dispose(); session.Dispose(); Assert.AreEqual(2, backend.Unregistered.Count);
        Assert.IsFalse(session.TryDispatch(new(show.Id), MessageData(show.Key)));
        using var replacement = factory.Create(new(42), settings, actions.Add);
        Assert.IsFalse(replacement.TryDispatch(new(show.Id), MessageData(show.Key)));
    }

    [TestMethod]
    public void GlobalHotkeyConflictWarnsInTheLogWindowAndLeavesTheOtherActionUsable()
    {
        var backend = new HotkeyBackendDouble { RejectedKey = 0x72 };
        var logs = new LogWindowService();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new UiLogProvider(logs)));
        var factory = new AppHotkeyFactory(backend, new(false), loggerFactory.CreateLogger<AppHotkeyFactory>());
        var actions = new List<AppHotkeyAction>();
        using var session = factory.Create(new(42), new() { ShowWindowHotkey = "Ctrl+Alt+F3", DialHotkey = "Ctrl+Alt+F4" }, actions.Add);
        var warning = logs.Drain().Single(); Assert.AreEqual(LogLevel.Warning, warning.Level); Assert.Contains("Tastenkürzel", warning.Message);
        Assert.IsFalse(session.TryDispatch(new(backend.Registered[0].Id), MessageData(0x72)));
        Assert.IsTrue(session.TryDispatch(new(backend.Registered[1].Id), MessageData(0x73)));
        Assert.AreEqual(AppHotkeyAction.OpenDial, actions.Single());
        backend.FailRelease = true; session.Dispose();
        Assert.AreEqual(1, backend.Unregistered.Count); Assert.Contains("nicht abgemeldet", logs.Drain().Single().Message);
    }

    [TestMethod]
    public void GlobalHotkeysNeverReserveKeysInDiagnosticsOrWithDefaultSettings()
    {
        var backend = new HotkeyBackendDouble();
        using var audit = new AppHotkeyFactory(backend, new(true), NullLogger<AppHotkeyFactory>.Instance)
            .Create(new(42), new() { DialHotkey = "Ctrl+Alt+F3" }, _ => Assert.Fail());
        using var defaults = new AppHotkeyFactory(backend, new(false), NullLogger<AppHotkeyFactory>.Instance)
            .Create(new(42), new(), _ => Assert.Fail());
        Assert.AreEqual(0, backend.Registered.Count);
    }

    [TestMethod]
    public async Task GlobalHotkeyNativeRegistrationDetectsCollisionAndCanBeReclaimedAfterRelease()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var first = new NativeWindow(); var second = new NativeWindow();
            var backend = new WindowsAppHotkeyBackend(); var key = 0; var secondOwned = false;
            try
            {
                first.CreateHandle(new CreateParams { Parent = new IntPtr(-3) });
                second.CreateHandle(new CreateParams { Parent = new IntPtr(-3) });
                for (var candidate = 0x70; candidate <= 0x7A; candidate++)
                    if (backend.Register(first.Handle, 1, 0x4003, candidate)) { key = candidate; break; }
                Assert.AreNotEqual(0, key, "No free Ctrl+Alt+F1..F11 combination was available for the isolated native check.");
                Assert.IsFalse(backend.Register(second.Handle, 1, 0x4003, key));
                Assert.IsTrue(backend.Unregister(first.Handle, 1));
                secondOwned = backend.Register(second.Handle, 1, 0x4003, key); Assert.IsTrue(secondOwned);
                completion.SetResult();
            }
            catch (Exception exception) { completion.TrySetException(exception); }
            finally
            {
                if (secondOwned) backend.Unregister(second.Handle, 1);
                if (key != 0) backend.Unregister(first.Handle, 1);
                second.DestroyHandle(); first.DestroyHandle();
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10)); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2)));
    }

    internal static IntPtr MessageData(int key, int modifiers = 3) => new((key << 16) | modifiers);
}

internal sealed class HotkeyBackendDouble : IAppHotkeyBackend
{
    internal readonly List<(IntPtr Window, int Id, int Modifiers, int Key)> Registered = [];
    internal readonly List<int> Unregistered = [];
    internal int RejectedKey;
    internal bool FailRelease;
    public bool Register(IntPtr window, int id, int modifiers, int key)
    { Registered.Add((window, id, modifiers, key)); return key != RejectedKey; }
    public bool Unregister(IntPtr window, int id) { Unregistered.Add(id); return !FailRelease; }
}
