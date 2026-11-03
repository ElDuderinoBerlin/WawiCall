using System.Reflection;

namespace WawiCallPro.Utilities;

public static class ApplicationIdentity
{
    public const string Name = "WawiCallPro";

    public static string GetProductLine(Assembly assembly)
    {
        return assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "LexwareProductLine")?.Value ?? "Nicht festgelegt";
    }
}
