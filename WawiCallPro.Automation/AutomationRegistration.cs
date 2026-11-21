using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Automation;

public interface IAutomationApplicationFactory { AutomationApplication Create(); }

internal sealed class AutomationApplicationFactory(AutomationOperations operations, IAutomationEnvironment environment, IAutomationTelephony telephony) : IAutomationApplicationFactory
{
    public AutomationApplication Create() => environment.GetStatus().ProductLine switch
    {
        LexwareProductLine.Pro => new ProApplication(operations, environment, telephony),
        LexwareProductLine.FinOffice => new FinOfficeApplication(operations, environment, telephony),
        _ => throw new AutomationFault(AutomationFailure.AccessDenied)
    };
}

public static class AutomationRegistration
{
    /// <summary>Host supplies environment, IPartySearch, presentation and telephony. Shared queue belongs to this container.</summary>
    public static IServiceCollection AddWawiAutomation(this IServiceCollection services, AutomationOptions? options = null)
    {
        var policy = options ?? new(); policy.Validate();
        services.AddSingleton(policy);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<NumberedPartyResolver>();
        services.TryAddSingleton<IAutomationBusinessAccess, EnvironmentBusinessAccess>();
        services.AddSingleton<AutomationRouter>();
        services.AddSingleton(provider => new AutomationOperations(policy, provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<AutomationRouter>()));
        services.AddSingleton<IAutomationApplicationFactory, AutomationApplicationFactory>();
        return services;
    }
}
