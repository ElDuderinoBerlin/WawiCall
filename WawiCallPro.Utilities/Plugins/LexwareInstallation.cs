using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;
using Microsoft.Win32;
#if LX_PRO
using Quantarix.Lexware.Core.Registry;
#else
using Quantarix.Pcfk.Core.Registry;
#endif
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Plugins;

/// <summary>Read-only boundary around the existing vendor registry checks; each product is checked independently.</summary>
public interface ILexwareRegistryProbe
{
    bool IsProInstalled();
    bool IsFinOfficeInstalled();
}

public sealed class LexwareRegistryProbe : ILexwareRegistryProbe
{
    public bool IsProInstalled()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
#if LX_PRO
        return LexwareRegistry.IsInstalled();
#else
        // Same marker as LexwareRegistry.IsInstalled; no Pro assembly in FinOffice output.
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Lexware\Lexware professional\Verzeichnisse");
        return key is not null;
#endif
    }
    public bool IsFinOfficeInstalled()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
#if LX_FINOFFICE
        return PcfkRegistry.IsInstalled();
#else
        return HasFinOfficeMarker();
#endif
    }
#if LX_PRO
    [SupportedOSPlatform("windows")]
    private static bool HasFinOfficeMarker()
    {
        // Match PcfkRegistry's Registry32 probe and explicit WOW6432Node fallback.
        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using var key = machine.OpenSubKey(@"SOFTWARE\Lexware\financial office");
            if (key is not null) return true;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        using var fallback = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Lexware\financial office");
        return fallback is not null;
    }
#endif
}

/// <summary>Registry markers alone do not establish a complete or working installation.</summary>
public sealed record LexwareInstallationState(bool ProInstalled, bool FinOfficeInstalled, bool CheckFailed)
{
    public bool IsAvailable => !CheckFailed && (ProInstalled || FinOfficeInstalled);
}

public interface ILexwareInstallation
{
    LexwareInstallationState Current { get; }
    LexwareInstallationState Check();
    void EnsureAvailable();
}

/// <summary>Fail-closed, edition-independent prerequisite. Warnings use only the closed, safe logging vocabulary.</summary>
public sealed class LexwareInstallation(ILexwareRegistryProbe registry, ILogger<LexwareInstallation> log) : ILexwareInstallation
{
    private readonly Lock _gate = new();
    private LexwareInstallationState _current = new(false, false, true);
    private bool _checked;
    public LexwareInstallationState Current => Volatile.Read(ref _current);

    public LexwareInstallationState Check()
    {
        lock (_gate)
        {
            var failed = false;
            var pro = false;
            var finOffice = false;
            try { pro = registry.IsProInstalled(); } catch { failed = true; }
            try { finOffice = registry.IsFinOfficeInstalled(); } catch { failed = true; }
            var observed = new LexwareInstallationState(pro, finOffice, failed);
            if ((!_checked || observed != _current) && !observed.IsAvailable)
                OperationEvents.Write(log, failed ? OperationEvent.LexwareCheckFailed : OperationEvent.LexwareMissing);
            Volatile.Write(ref _current, observed);
            _checked = true;
            return observed;
        }
    }

    public void EnsureAvailable()
    {
        if (!Check().IsAvailable) throw new LexwareUnavailableException();
    }
}

public sealed class LexwareUnavailableException() : Exception("Lexware Pro oder Financial Office wurde nicht erkannt oder die Installationsprüfung ist fehlgeschlagen. Lexware-Funktionen bleiben gesperrt; Details stehen im Log-Fenster.");
