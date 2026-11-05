namespace WawiCallPro.App.Hosting;

/// <summary>Composition-root test support; background message-loop tests never display or focus a window.</summary>
internal sealed record AppWindowOptions(bool Hidden = false);
