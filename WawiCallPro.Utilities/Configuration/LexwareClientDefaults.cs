using System.Reflection;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Local build default selected in Solution.props. No personal settings or source installation is read.</summary>
public static class LexwareClientDefaults
{
    public static string? GetProClientKey()
    {
        var value = typeof(LexwareClientDefaults).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == "WawiCallPro.LexwareApiClientKey")?.Value;
        return Guid.TryParseExact(value, "B", out var key) && key != Guid.Empty ? value : null;
    }
}
