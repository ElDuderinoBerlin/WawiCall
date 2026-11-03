using System.Reflection;

namespace WawiCallPro.Utilities.Hosting;

/// <summary>Host identity comes from the entry assembly, never from the shared utilities assembly.</summary>
public sealed record ApplicationDescriptor(string Name, string ProductLine)
{
    public static ApplicationDescriptor FromAssembly(Assembly entryAssembly) =>
        new(ApplicationIdentity.Name, ApplicationIdentity.GetProductLine(entryAssembly));
}
