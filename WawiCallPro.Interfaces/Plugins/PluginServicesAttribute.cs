using Microsoft.Extensions.DependencyInjection;

namespace WawiCallPro.Interfaces.Plugins;

/// <summary>Optional plugin-owned DI registrations, evaluated before entry-point activation.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PluginServicesAttribute(Type moduleType) : Attribute
{
    public Type ModuleType { get; } = moduleType;
}

/// <summary>Registers private services in a container owned and released by the plugin session.</summary>
public interface IPluginServiceModule
{
    void ConfigureServices(IServiceCollection services);
}
