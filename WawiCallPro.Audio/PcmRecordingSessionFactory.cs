using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

public sealed class PcmRecordingSessionFactory(IPcmSegmentSinkFactory sinks, IConversationWaveMixer mixer) : IPcmRecordingSessionFactory
{
    public async Task<IPcmRecordingSession> StartAsync(PcmRecordingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.QueueCapacity is < 1 or > 256) throw new ArgumentException("Die Audiopuffergröße muss zwischen 1 und 256 liegen.");
        ConversationWaveMixValidation.Validate(new(request.CallId, request.RecordingId, [], request.WavePath,
            request.SampleRate, TimeSpan.FromMilliseconds(1), request.Layout));
        cancellationToken.ThrowIfCancellationRequested();
        var session = new PcmRecordingSession(request, sinks, mixer, cancellationToken);
        try { await session.Ready; return session; }
        catch { await session.DisposeAsync(); throw; }
    }
}
