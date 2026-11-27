using SIPSorceryMedia.Abstractions;

namespace WawiCallPro.Audio;

/// <summary>Softens the transition from digital silence on a newly opened playback device.</summary>
internal sealed class ConversationPlaybackEncoder(IAudioEncoder inner) : IAudioEncoder
{
    private const int FadeMilliseconds = 15;
    private int _fadePosition;

    public List<AudioFormat> SupportedFormats => inner.SupportedFormats;
    public byte[] EncodeAudio(short[] pcm, AudioFormat format) => inner.EncodeAudio(pcm, format);

    public short[] DecodeAudio(byte[] encoded, AudioFormat format)
    {
        // The underlying encoder publishes original PCM to the RTP recording and meter before playback.
        var decoded = inner.DecodeAudio(encoded, format);
        var channels = format.ChannelCount;
        var fadeFrames = format.ClockRate * FadeMilliseconds / 1000;
        if (fadeFrames < 2 || channels is not (1 or 2) || decoded.Length % channels != 0 ||
            _fadePosition >= fadeFrames || decoded.Length == 0)
            return decoded;

        var first = 0;
        if (_fadePosition == 0)
        {
            while (first < decoded.Length && decoded[first] == 0) first++;
            if (first == decoded.Length) return decoded;
            first -= first % channels;
        }
        var playback = (short[])decoded.Clone();
        for (var frame = first; frame < playback.Length && _fadePosition < fadeFrames; frame += channels)
        {
            for (var channel = 0; channel < channels; channel++)
                playback[frame + channel] = (short)(playback[frame + channel] * _fadePosition / (fadeFrames - 1));
            _fadePosition++;
        }
        return playback;
    }
}
