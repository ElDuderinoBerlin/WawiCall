using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Synchronous CSCore boundary, called on a worker with a validated request.
/// The caller owns the seekable output stream; return only after WAV finalization and source release.</summary>
public interface IConversationWaveRenderer
{
    ConversationWaveMixResult Render(ConversationWaveMixRequest request, Stream output, CancellationToken cancellationToken);
}
