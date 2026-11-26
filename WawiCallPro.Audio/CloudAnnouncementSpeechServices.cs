using Azure.Core;
using Azure.Identity;
using Google.Cloud.TextToSpeech.V1;
using Google.Apis.Auth.OAuth2;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

/// <summary>Registers optional cloud providers alongside the local announcement synthesis service.</summary>
public static class CloudAnnouncementSpeechServices
{
    public const string AzureSpeech = "AzureSpeech";
    public const string GoogleCloud = "GoogleCloud";

    /// <summary>Preset voices available for selection before a provider's catalog has been loaded.</summary>
    public static IReadOnlyList<AnnouncementVoice> GetPresetVoices(AnnouncementSpeechProvider provider) => provider switch
    {
        AnnouncementSpeechProvider.AzureSpeech => AzureAnnouncementSpeech.Voices,
        AnnouncementSpeechProvider.GoogleCloud => GoogleAnnouncementSpeech.Voices,
        _ => []
    };

    /// <summary>Creates the same Azure adapter with a regional resource key.</summary>
    public static IAnnouncementSpeech CreateAzureWithKey(string region, string key) =>
        new AzureAnnouncementSpeech(() => SpeechConfig.FromSubscription(key, region));

    /// <summary>Creates the Google adapter for the specified project, using an optional service account or local ADC.</summary>
    public static IAnnouncementSpeech CreateGoogleForProject(string project, GoogleCredential? credential) =>
        new GoogleAnnouncementSpeech(BuildGoogleClient(project, credential));

    /// <summary>Accepts only a Google service account credential with Google's token endpoint.</summary>
    public static GoogleCredential ReadGoogleServiceAccount(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("token_uri", out var tokenUri)
            || tokenUri.ValueKind != JsonValueKind.String || tokenUri.GetString() != "https://oauth2.googleapis.com/token"
            || (root.TryGetProperty("universe_domain", out var universe)
                && (universe.ValueKind != JsonValueKind.String || universe.GetString() != "googleapis.com")))
            throw new FormatException("Die Datei enthält keine unterstützten Google-Dienstkonto-Anmeldedaten.");
        return GoogleCredential.FromServiceAccountCredential(CredentialFactory.FromJson<ServiceAccountCredential>(json));
    }

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
            (_, _) => BuildGoogleClient(quotaProject, null));
        services.TryAddKeyedSingleton<IAnnouncementSpeech>(GoogleCloud,
            (provider, key) => new GoogleAnnouncementSpeech(provider.GetRequiredKeyedService<TextToSpeechClient>(key)));
        return services;
    }

    private static TextToSpeechClient BuildGoogleClient(string project, GoogleCredential? credential) =>
        new TextToSpeechClientBuilder
        {
            QuotaProject = project,
            GoogleCredential = credential,
            Settings = new TextToSpeechSettings
            {
                // Reserve each billable attempt explicitly. Don't let GAX retry synthesis behind the limiter.
                SynthesizeSpeechSettings = CallSettings.FromExpiration(Expiration.FromTimeout(TimeSpan.FromSeconds(30)))
            }
        }.Build();
}
