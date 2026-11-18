using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Interfaces.Office;

namespace WawiCallPro.Office;

public static class MailServiceRegistration
{
    public static IServiceCollection AddMailConnectors(this IServiceCollection services)
    {
        services.AddSingleton<ProtectedMailData>(); services.AddSingleton<MailTokenStore>();
        services.AddSingleton<IMailAuthentication, MailAuthentication>();
        services.AddSingleton(_ => MailProviderHttpClient.Create());
        services.AddSingleton(provider => new MailHttp(provider.GetRequiredService<MailProviderHttpClient>(), provider.GetRequiredService<IMailAuthentication>()));
        services.AddSingleton<IMailConnectorFactory, MailConnectorFactory>();
        services.AddSingleton<LxPrintStudioMailImport>(); services.AddSingleton<MailAccounts>();
        services.AddSingleton<DirectMailViewSource>();
        services.AddSingleton<IMailBlobProtector, ProtectedMailBlob>();
        services.AddSingleton<MailCacheService>();
        services.AddSingleton<IMailSynchronization>(provider => provider.GetRequiredService<MailCacheService>());
        services.AddSingleton<IMailViewSource>(provider => provider.GetRequiredService<MailCacheService>());
        services.AddSingleton<IMailCacheMaintenance>(provider => provider.GetRequiredService<MailCacheService>());
        services.AddSingleton<MailApplication>();
        return services;
    }
}

internal sealed class MailProviderHttpClient() : HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
{
    public static MailProviderHttpClient Create() => new() { Timeout = TimeSpan.FromSeconds(60) };
}
