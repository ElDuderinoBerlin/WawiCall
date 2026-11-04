using System.Reflection;
using System.Runtime.Loader;
using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Plugins;
using System.Collections.Immutable;

namespace WawiCallPro.Utilities.Plugins;

public interface ISharedPluginContracts
{
    void EnsureLoaded();
    IReadOnlySet<string> GetSharedPackageAssemblyNames();
}

/// <summary>Explicit contract roots, evaluated before Quantarix snapshots the default load context.</summary>
public sealed record SharedPluginContractAssembly(Assembly Assembly);

public sealed class SharedPluginContracts(IEnumerable<SharedPluginContractAssembly> additional) : ISharedPluginContracts
{
    public void EnsureLoaded()
    {
        foreach (var assembly in new[] { typeof(IPlugin).Assembly, typeof(IWawiCallPlugin).Assembly }.Concat(additional.Select(item => item.Assembly)))
            if (AssemblyLoadContext.GetLoadContext(assembly) != AssemblyLoadContext.Default || assembly.GetCustomAttribute<SharedAssemblyAttribute>() is null)
                throw new InvalidOperationException("Plugin contracts must be shared assemblies in the default load context.");
        foreach (var name in GetSharedPackageAssemblyNames()) AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(name));
    }
    public IReadOnlySet<string> GetSharedPackageAssemblyNames()
    {
        using var stream = typeof(SharedPluginContracts).Assembly.GetManifestResourceStream("WawiCallPro.SharedPackages") ?? throw new PluginRuntimeException();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
