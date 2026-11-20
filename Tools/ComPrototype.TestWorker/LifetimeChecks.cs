using System.Runtime.InteropServices;
using System.Text.Json;

namespace WawiCallPro.ComPrototype;

internal static class LifetimeChecks
{
    internal static int Run(string output)
    {
        var passed = new List<string>();
        try
        {
            var clock = new TestClock();
            var lifetime = new ServerLifetime(clock, TimeSpan.FromSeconds(2));
            clock.Advance(); Check(!lifetime.TryBeginShutdown(), "startup holds host", passed);
            lifetime.CompleteStartup();
            Check(!lifetime.TryBeginShutdown(), "idle grace", passed);
            var first = lifetime.AcquireClient(); var second = lifetime.AcquireClient();
            first.Dispose(); first.Dispose(); clock.Advance();
            Check(lifetime.Clients == 1 && !lifetime.TryBeginShutdown(), "independent clients and double release", passed);
            lifetime.SetUserOwned(true); second.Dispose(); clock.Advance();
            Check(!lifetime.TryBeginShutdown(), "user ownership survives last client", passed);
            lifetime.SetCallActive(true); lifetime.SetUserOwned(false); clock.Advance();
            Check(!lifetime.TryBeginShutdown(), "active call survives last client", passed);
            lifetime.LockServer(true); lifetime.SetCallActive(false); clock.Advance();
            Check(!lifetime.TryBeginShutdown(), "class factory lock survives last client", passed);
            lifetime.LockServer(false); clock.Advance();
            Check(lifetime.TryBeginShutdown() && !lifetime.TryBeginShutdown(), "one shutdown decision", passed);
            try { lifetime.AcquireClient(); throw new Exception("Late activation accepted."); }
            catch (InvalidOperationException) { passed.Add("late activation refused"); }

            var concurrent = new ServerLifetime(clock, TimeSpan.FromSeconds(2)); concurrent.CompleteStartup();
            Parallel.For(0, 1000, _ => { using var lease = concurrent.AcquireClient(); });
            Check(concurrent.Clients == 0, "parallel acquisition and release balanced", passed);

            var dependency = new ProbeDependency();
            using var dispatcherControl = new Control();
            _ = dispatcherControl.Handle;
            var dispatcher = new Quantarix.Interop.Dispatcher.WinFormsUiDispatcher(dispatcherControl);
            var factory = new InjectedClassFactory(() => new PrototypeAutomation(dependency, concurrent, dispatcher), concurrent);
            var unsupported = Guid.NewGuid();
            try { factory.CreateInstance(null!, ref unsupported, out _); throw new Exception("Unknown IID accepted."); }
            catch (COMException error) when (error.HResult == unchecked((int)0x80004002))
            { Check(concurrent.Clients == 0, "unsupported IID creates no session", passed); }

            var facade = new PrototypeAutomation(dependency, concurrent, dispatcher);
            var unknown = Marshal.GetIUnknownForObject(facade);
            try
            {
                var safety = new Guid("CB5BDC81-93C1-11CF-8F20-00805F2CD064");
                var hr = Marshal.QueryInterface(unknown, in safety, out var pointer);
                if (pointer != IntPtr.Zero) Marshal.Release(pointer);
                Check(hr == unchecked((int)0x80004002), "no inherited untrusted scripting safety", passed);
            }
            finally { Marshal.Release(unknown); facade.Close(); }

            File.WriteAllText(output, JsonSerializer.Serialize(new { Passed = passed }));
            return 0;
        }
        catch (Exception error) { File.WriteAllText(output, JsonSerializer.Serialize(new { Passed = passed, Error = error.ToString() })); return 1; }
    }

    private static void Check(bool result, string name, List<string> passed)
    { if (!result) throw new InvalidOperationException(name); passed.Add(name); }
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance() => _now = _now.AddSeconds(3);
    }
}
