using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

public static class AudioConversionServices
{
    /// <summary>Both hosts share a stateless converter. Native encoders, resamplers and files are per conversion.</summary>
    public static IServiceCollection AddAudioConversion(this IServiceCollection services)
    {
        services.TryAddSingleton<IMp3EncodingBackend, MediaFoundationMp3Backend>();
        services.TryAddSingleton<IMp3Converter, WavToMp3Converter>();
        return services;
    }
}
