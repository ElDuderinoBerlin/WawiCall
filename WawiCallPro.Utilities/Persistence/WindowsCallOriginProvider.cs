using System.Security;
using System.Security.Principal;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Utilities.Persistence;

public sealed class WindowsCallOriginProvider(IWorkstationIdentity workstation) : ICallOriginProvider
{
    public async Task<CallOrigin> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var id = await workstation.GetAsync(cancellationToken).ConfigureAwait(false)
            ?? throw ConfigurationException.FileError("Die Arbeitsplatzkennung fehlt. Zuerst die Einrichtung mit --workstation-init abschließen.");
        if (!OperatingSystem.IsWindows()) throw ConfigurationException.FileError("Die Windows-Gesprächsherkunft ist auf diesem System nicht verfügbar.");
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            cancellationToken.ThrowIfCancellationRequested();
            return new(id, Environment.MachineName, identity.Name);
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or ArgumentException)
        { throw ConfigurationException.FileError("Die Windows-Gesprächsherkunft konnte nicht erfasst werden."); }
    }
}
