using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Telephony.Sip;

public static class SipServiceRegistration
{
    /// <summary>Registers one shared runtime; CLI utilities opt out of automatic account ownership.</summary>
    public static IServiceCollection AddSipTelephony(this IServiceCollection services, bool allowAutomaticRegistration)
    {
        services.TryAddSingleton<ISipTransportFactory, SipTransportFactory>();
        services.TryAddSingleton<ISipAccountLeaseFactory, SipAccountLeaseFactory>();
        services.TryAddSingleton<ISipMediaSessionFactory, UnavailableSipMediaSessionFactory>();
        services.TryAddSingleton<ISipCallPersistence, SipCallPersistence>();
        services.TryAddSingleton<ICallStateSessionFactory, CallStateSessionFactory>();
        services.AddSingleton<SipTelephonyService>();
        services.AddSingleton<ITelephonyService>(provider => provider.GetRequiredService<SipTelephonyService>());
        services.AddSingleton<ITelephonyConnection>(provider => provider.GetRequiredService<SipTelephonyService>());
        services.AddSingleton<IDtmfReceiver>(provider => provider.GetRequiredService<SipTelephonyService>());
        services.AddSingleton(new SipHostOptions(allowAutomaticRegistration));
        services.AddHostedService<SipHostService>();
        return services;
    }
}

internal sealed record SipHostOptions(bool AllowAutomaticRegistration);

internal sealed class SipHostService(SipTelephonyService telephony, ApplicationConfigurationService settings, SipHostOptions options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.AllowAutomaticRegistration || settings.Current.Options.SipAccount?.RegisterOnStartup != true) return;
        try { await telephony.ConnectAsync(cancellationToken).ConfigureAwait(false); }
        catch (TelephonyException) { /* Registration status keeps the failure visible while configuration remains available. */ }
    }
    public Task StopAsync(CancellationToken cancellationToken) => telephony.DisconnectAsync(cancellationToken);
}
