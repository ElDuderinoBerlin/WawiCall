using Azure.Core;
using Azure.Identity;
using Google.Cloud.TextToSpeech.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WawiCallPro.Audio;

/// <summary>Registers optional cloud providers alongside the local announcement synthesis service.</summary>
public static class CloudAnnouncementSpeechServices
{
    public const string AzureSpeech = "AzureSpeech";
    public const string GoogleCloud = "GoogleCloud";

    /// <summary>Registers Azure Speech for a public Azure resource with a custom endpoint and Entra authentication.</summary>
    public static IServiceCollection AddAzureAnnouncementSpeech(this IServiceCollection services, Uri endpoint,
        TokenCredential? credential = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps || !endpoint.IsDefaultPort
            || !endpoint.Host.EndsWith(".cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase)
            || endpoint.PathAndQuery != "/" || endpoint.UserInfo.Length != 0 || endpoint.Fragment.Length != 0)
            throw new ArgumentException("Ein benutzerdefinierter HTTPS-Endpunkt der Azure-Speech-Ressource ist erforderlich.", nameof(endpoint));
        services.TryAddKeyedSingleton<IAnnouncementSpeech>(AzureSpeech,
            (_, _) => new AzureAnnouncementSpeech(endpoint, credential ?? new DefaultAzureCredential()));
        return services;
    }

    /// <summary>Registers Google Cloud synthesis with Application Default Credentials and an explicit quota project.</summary>
    public static IServiceCollection AddGoogleAnnouncementSpeech(this IServiceCollection services, string quotaProject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quotaProject);
        services.TryAddKeyedSingleton<TextToSpeechClient>(GoogleCloud,
            (_, _) => new TextToSpeechClientBuilder { QuotaProject = quotaProject }.Build());
        services.TryAddKeyedSingleton<IAnnouncementSpeech>(GoogleCloud,
            (provider, key) => new GoogleAnnouncementSpeech(provider.GetRequiredKeyedService<TextToSpeechClient>(key)));
        return services;
    }
}
