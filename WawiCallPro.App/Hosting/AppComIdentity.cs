using System.IO;
using System.Text.Json;
using WawiCallPro.Automation.Contracts;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App.Hosting;

/// <summary>Product identities use the regular profile; isolated profiles may only publish their own random class.</summary>
internal sealed record AppComIdentity(Guid ClassId, bool Enabled)
{
    internal string? FixtureDirectory { get; init; }
    internal static AppComIdentity Resolve(AppStartArguments start, ApplicationDescriptor application)
    {
        if (start.FixtureDirectory is { } fixture)
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture, "com-fixture.json")));
            var id = json.RootElement.GetProperty("ClassId").GetGuid();
            if (id == Guid.Empty || id == new Guid(AutomationIds.ProClass) || id == new Guid(AutomationIds.FinOfficeClass)
                || json.RootElement.GetProperty("ProgId").GetString() != "WCP.p" + id.ToString("N"))
                throw new ArgumentException("Invalid isolated COM identity.");
            Environment.SetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR", Path.GetFullPath(fixture));
            return new(id, true) { FixtureDirectory = Path.GetFullPath(fixture) };
        }
        var overridden = Environment.GetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR") is not null;
        if (start.Kind == AppStartKind.Com && overridden) throw new ArgumentException("COM requires the regular profile or an isolated fixture identity.");
        return new(new Guid(application.ProductLine == "Pro" ? AutomationIds.ProClass : AutomationIds.FinOfficeClass), !overridden);
    }
}
