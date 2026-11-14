using System.Collections.Immutable;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Synchronous native boundary, called only on a worker. Each call owns its encoder/resampler.
/// Streams belong to the caller and must remain open. Encode returns only after successful finalization.</summary>
public interface IMp3EncodingBackend
{
    ImmutableArray<Mp3EncodingFormat> GetSupportedFormats(CancellationToken cancellationToken);
    TimeSpan Encode(Stream wave, Stream mp3, Mp3EncodingFormat format, CancellationToken cancellationToken);
}
