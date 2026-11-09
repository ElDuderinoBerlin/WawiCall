namespace WawiCallPro.App.Hosting;

internal static class StartupFocusHandoff
{
    internal static async Task CompleteAsync(Func<bool> ownsForeground, Func<Task> closeSplash, Action activateMain)
    {
        // Keep the splash's STA input queue alive until the main window receives our foreground.
        try
        {
            if (ownsForeground()) activateMain();
        }
        finally { await closeSplash(); }
    }
}
