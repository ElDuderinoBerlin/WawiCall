using System.IO;
using System.Text.Json;
using Velopack;
using Velopack.Locators;

namespace WawiCallPro.App.Hosting;

/// <summary>Fast, dependency-light installation composition root. No application host is built by these callbacks.</summary>
internal static class AppVelopackBootstrap
{
    internal static void Run() => VelopackApp.Build().SetAutoApplyOnStartup(false)
        .OnAfterInstallFastCallback(version => Installed(version.ToString(), false))
        .OnAfterUpdateFastCallback(version => Installed(version.ToString(), true))
        .OnBeforeUninstallFastCallback(version => Removing(version.ToString()))
        .OnFirstRun(version => IsolatedFirstRun(version.ToString()))
        .Run();

    private static void IsolatedFirstRun(string version)
    {
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "wawicallpro.com-fixture.json"))) return;
        try
        {
            // Real Setup.exe tests use a validated random package/class instead of a visible first run.
            // Production packages reject this marker; a product package cannot masquerade as the fixture.
            Resolve(VelopackLocator.Current, version);
            Environment.Exit(0);
        }
        catch (Exception) { Environment.Exit(1); }
    }

    private static void Installed(string version, bool allowRollback) => Execute(version, true, allowRollback);
    private static void Removing(string version) => Execute(version, false, false);
    private static void Execute(string version, bool register, bool allowRollback)
    {
        try
        {
            var locator = VelopackLocator.Current;
            var identity = Resolve(locator, version);
            var service = new UserComRegistration(new WindowsUserComRegistry());
            if (register) service.Register(identity, allowRollback);
            else service.Unregister(identity);
        }
        catch (Exception)
        {
            Console.Error.WriteLine("WawiCallPro: COM-Installationsregistrierung fehlgeschlagen.");
            // Velopack exits from Run; do not let its callback exception handling turn this failure into exit 0.
            Environment.Exit(1);
        }
    }

    internal static ComRegistrationIdentity Resolve(IVelopackLocator locator, string hookVersion)
    {
        if (locator.CurrentlyInstalledVersion is null || locator.IsPortable || locator.RootAppDir is null
            || !SamePath(locator.AppContentDir, AppContext.BaseDirectory)) throw new InvalidOperationException("Installed app identity required.");
        var fixtureFile = Path.Combine(AppContext.BaseDirectory, "wawicallpro.com-fixture.json");
        string? profile = null; string? fixturePackage = null;
        if (File.Exists(fixtureFile))
        {
            using var fixture = JsonDocument.Parse(File.ReadAllText(fixtureFile));
            profile = fixture.RootElement.GetProperty("Profile").GetString() ?? throw new InvalidOperationException("Fixture profile missing.");
            fixturePackage = fixture.RootElement.GetProperty("PackageId").GetString();
        }
        var identity = AppComDeployment.Current(profile, locator.RootAppDir);
        var packageId = profile is null ? "WawiCallPro." + identity.ProductLine : "WawiCallPro.ComFixture.p" + identity.ClassId.ToString("N");
        if (profile is not null && fixturePackage != packageId || locator.AppId != packageId
            || ComRegistrationVersion.Compare(hookVersion, identity.Version) != 0
            || ComRegistrationVersion.Compare(locator.CurrentlyInstalledVersion.ToString(), identity.Version) != 0)
            throw new InvalidOperationException("Installed package and executable identities do not match.");
        return identity;
    }
    private static bool SamePath(string? a, string b) => a is not null && string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
}
