using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

public static class AudioConversionServices
{
    /// <summary>Both hosts share stateless conversion/mixing services. Native resources and files belong to each request.</summary>
    public static IServiceCollection AddAudioConversion(this IServiceCollection services)
    {
        services.TryAddSingleton<IMp3EncodingBackend, MediaFoundationMp3Backend>();
        services.TryAddSingleton<IMp3Converter, WavToMp3Converter>();
        services.TryAddSingleton<IConversationWaveRenderer, ConversationWaveRenderer>();
        services.TryAddSingleton<IConversationWaveMixer, ConversationWaveMixer>();
        services.TryAddSingleton<IPcmSegmentSinkFactory, CscorePcmSegmentSinkFactory>();
        services.TryAddSingleton<IPcmRecordingSessionFactory, PcmRecordingSessionFactory>();
        return services;
    }
}
