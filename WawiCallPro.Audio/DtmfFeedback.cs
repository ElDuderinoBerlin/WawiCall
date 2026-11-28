using System.Buffers.Binary;
using CSCore;
using CSCore.Codecs.RAW;
using DtmfDetection;

namespace WawiCallPro.Audio;

internal static class DtmfFeedback
{
    internal static IWaveSource CreateTone(char digit)
    {
        var tone = digit switch
        {
            '0' => DtmfTone.Zero, '1' => DtmfTone.One, '2' => DtmfTone.Two, '3' => DtmfTone.Three,
            '4' => DtmfTone.Four, '5' => DtmfTone.Five, '6' => DtmfTone.Six, '7' => DtmfTone.Seven,
            '8' => DtmfTone.Eight, '9' => DtmfTone.Nine, '*' => DtmfTone.Star, '#' => DtmfTone.Hash,
            'A' => DtmfTone.A, 'B' => DtmfTone.B, 'C' => DtmfTone.C, 'D' => DtmfTone.D,
            _ => throw new ArgumentOutOfRangeException(nameof(digit))
        };
        const int rate = 16000;
        const int toneFrames = rate * 120 / 1000;
        const int rampFrames = rate * 5 / 1000;
        var pcm = new byte[toneFrames * sizeof(short)];
        for (var frame = 0; frame < toneFrames; frame++)
        {
            var time = frame / (double)rate;
            var envelope = Math.Min(1, Math.Min(frame, toneFrames - 1 - frame) / (double)rampFrames);
            var sample = Math.Sin(Math.Tau * tone.LowTone * time) + Math.Sin(Math.Tau * tone.HighTone * time);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(frame * sizeof(short)), (short)(sample * envelope * 7000));
        }
        return new RawDataReader(new MemoryStream(pcm, writable: false), new WaveFormat(rate, 16, 1));
    }
}
