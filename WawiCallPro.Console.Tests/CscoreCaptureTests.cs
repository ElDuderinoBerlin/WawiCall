using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
using CSCore;
using CSCore.SoundIn;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows.CSCore;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class CscoreCaptureTests
{
    [TestMethod]
    [DataRow(8000, SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(8000, SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(16000, SDPWellKnownMediaFormatsEnum.G722)]
    public void CaptureConvertsBytesToExactlyOnePcm16SampleSequence(int rate, SDPWellKnownMediaFormatsEnum codec)
    {
        var samples = Samples(rate / 50); var format = new AudioFormat(codec); var encoder = new Encoder(format);
        using var source = new Source(rate, 1, samples);
        var endpoint = Endpoint(source, encoder, format); EncodedAudioFrame? frame = null;
        endpoint.OnAudioSourceEncodedFrameReady += value => frame = value;
        Invoke(endpoint);
        Assert.AreEqual(1, encoder.Inputs.Count); CollectionAssert.AreEqual(samples, encoder.Inputs[0]);
        Assert.IsNotNull(frame); Assert.AreEqual(20u, frame.DurationMilliSeconds);
        Assert.AreSame(encoder.Payload, frame.EncodedAudio);
    }

    [TestMethod]
    public void ConsecutiveReadsDoNotAppendZerosOrReencodePreviouslyReadSamples()
    {
        var first = Samples(160); var second = Samples(80).Select(sample => (short)-sample).ToArray();
        var format = new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU); var encoder = new Encoder(format);
        using var source = new Source(8000, 1, first, second);
        var endpoint = Endpoint(source, encoder, format); var durations = new List<uint>();
        endpoint.OnAudioSourceEncodedFrameReady += frame => durations.Add(frame.DurationMilliSeconds);
        Invoke(endpoint);
        Assert.AreEqual(2, encoder.Inputs.Count); CollectionAssert.AreEqual(first, encoder.Inputs[0]); CollectionAssert.AreEqual(second, encoder.Inputs[1]);
        CollectionAssert.AreEqual(new uint[] { 20, 10 }, durations);
    }

    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public void ActualCaptureHandlerPreservesCodecPayloadsAndPcmTiming(SDPWellKnownMediaFormatsEnum codec)
    {
        var format = new AudioFormat(codec); var samples = Samples(format.ClockRate / 50);
        var native = new AudioEncoder(); var reference = new AudioEncoder();
        using var source = new Source(format.ClockRate, 1, samples, samples, samples);
        var endpoint = Endpoint(source, native, format); var captured = new List<EncodedAudioFrame>();
        endpoint.OnAudioSourceEncodedFrameReady += captured.Add;
        Invoke(endpoint);
        Assert.AreEqual(3, captured.Count);
        foreach (var frame in captured)
        {
            CollectionAssert.AreEqual(reference.EncodeAudio(samples, format), frame.EncodedAudio);
            Assert.AreEqual(20u, frame.DurationMilliSeconds); Assert.AreEqual(format.ClockRate, frame.AudioFormat.ClockRate);
            if (codec == SDPWellKnownMediaFormatsEnum.G722) Assert.AreEqual(8000, frame.AudioFormat.RtpClockRate);
        }
    }

    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public void LegacySampleDurationUsesRtpTicksInsteadOfPayloadByteLength(SDPWellKnownMediaFormatsEnum codec)
    {
        var format = new AudioFormat(codec); var encoder = new Encoder(format);
        using var source = new Source(format.ClockRate, 1, Samples(format.ClockRate / 50));
        var endpoint = Endpoint(source, encoder, format); uint? duration = null; byte[]? payload = null;
        endpoint.OnAudioSourceEncodedSample += (value, bytes) => { duration = value; payload = bytes; };
        Invoke(endpoint);
        Assert.AreEqual((uint)(format.RtpClockRate / 50), duration); Assert.AreSame(encoder.Payload, payload);
    }

    [TestMethod]
    public void InterleavedSamplesArePreservedWhileDurationCountsFramesPerChannel()
    {
        var format = new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU) { ChannelCount = 2 };
        var encoder = new Encoder(format); var samples = Samples(320);
        using var source = new Source(8000, 2, samples); var endpoint = Endpoint(source, encoder, format);
        EncodedAudioFrame? frame = null; uint? rtpTicks = null;
        endpoint.OnAudioSourceEncodedFrameReady += value => frame = value; endpoint.OnAudioSourceEncodedSample += (ticks, _) => rtpTicks = ticks;
        Invoke(endpoint);
        CollectionAssert.AreEqual(samples, encoder.Inputs.Single()); Assert.IsNotNull(frame); Assert.AreEqual(20u, frame.DurationMilliSeconds); Assert.AreEqual(160u, rtpTicks);
        // This isolates channel/frame arithmetic with a fake codec; it does not advertise stereo G.711 support.
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(1, 3)]
    [DataRow(2, 2)]
    public void PartialPcmSamplesOrChannelFramesFailBeforeEncoding(int channels, int byteCount)
    {
        var format = new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU) { ChannelCount = channels }; var encoder = new Encoder(format);
        using var source = new Source(8000, channels, new byte[byteCount]); var endpoint = Endpoint(source, encoder, format);
        var events = 0; endpoint.OnAudioSourceEncodedFrameReady += _ => events++;
        Assert.ThrowsExactly<InvalidOperationException>(() => Invoke(endpoint)); Assert.AreEqual(0, encoder.Inputs.Count); Assert.AreEqual(0, events);
    }

    [TestMethod]
    public void EmptyCaptureReadDoesNotProduceAnEncodedFrame()
    {
        var format = new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU); var encoder = new Encoder(format);
        using var source = new Source(8000, 1); var endpoint = Endpoint(source, encoder, format);
        var events = 0; endpoint.OnAudioSourceEncodedFrameReady += _ => events++; Invoke(endpoint);
        Assert.AreEqual(0, encoder.Inputs.Count); Assert.AreEqual(0, events);
    }

    [TestMethod]
    public async Task ActualCaptureFeedsTheWawiPcmObserverWithCorrectG722SampleCount()
    {
        var format = new AudioFormat(SDPWellKnownMediaFormatsEnum.G722); var samples = Samples(320);
        await using var tap = new PcmObservingAudioEncoder(new AudioEncoder()); var observer = new Observer();
        await using var subscription = tap.Subscribe(observer);
        using var source = new Source(16000, 1, samples); var endpoint = Endpoint(source, tap, format);
        Invoke(endpoint);
        Assert.AreEqual(ConversationAudioSide.Sent, observer.Side); Assert.AreEqual(16000, observer.Rate); CollectionAssert.AreEqual(samples, observer.Samples);
    }

    // Run only the real handler with injected synthetic fields. The normal constructor resolves
    // default Windows devices even when source and sink are disabled; deliberately do not call it.
    private static WindowsAudioEndPoint Endpoint(IWaveSource source, IAudioEncoder encoder, AudioFormat format)
    {
        var endpoint = (WindowsAudioEndPoint)RuntimeHelpers.GetUninitializedObject(typeof(WindowsAudioEndPoint));
        var manager = new MediaFormatManager<AudioFormat>([format]); manager.SetSelectedFormat(format);
        Field("_inputWaveSource").SetValue(endpoint, source); Field("_audioEncoder").SetValue(endpoint, encoder); Field("_audioFormatManager").SetValue(endpoint, manager);
        return endpoint;
    }
    private static FieldInfo Field(string name) => typeof(WindowsAudioEndPoint).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new AssertFailedException("CSCore capture test seam changed: " + name);
    private static void Invoke(WindowsAudioEndPoint endpoint)
    {
        var method = typeof(WindowsAudioEndPoint).GetMethod("LocalAudioSampleAvailable", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("CSCore capture handler changed.");
        try { method.Invoke(endpoint, [null, null]); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
    }
    private static short[] Samples(int count) => Enumerable.Range(0, count).Select(index => (short)(index % 2 == 0 ? 1234 + index : -1234 - index)).ToArray();
    private sealed class Encoder(AudioFormat format) : IAudioEncoder
    {
        internal List<short[]> Inputs { get; } = []; internal byte[] Payload = [7, 9, 11];
        public List<AudioFormat> SupportedFormats => [format];
        public byte[] EncodeAudio(short[] pcm, AudioFormat selected) { Inputs.Add(pcm.ToArray()); return Payload; }
        public short[] DecodeAudio(byte[] encoded, AudioFormat selected) => throw new NotSupportedException();
    }
    private sealed class Observer : IConversationPcmObserver
    {
        internal ConversationAudioSide Side; internal int Rate; internal short[] Samples = [];
        public bool TryObserve(ConversationAudioSide side, int rate, int channels, ReadOnlySpan<short> samples)
        { Side = side; Rate = rate; Samples = samples.ToArray(); return true; }
    }
    private sealed class Source : IWaveSource
    {
        private readonly Queue<byte[]> _chunks;
        internal Source(int rate, int channels, params short[][] chunks)
        {
            WaveFormat = new WaveFormat(rate, 16, channels);
            _chunks = new Queue<byte[]>(chunks.Select(samples =>
            {
                var bytes = new byte[samples.Length * 2];
                for (var index = 0; index < samples.Length; index++) BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2, 2), samples[index]);
                return bytes;
            }));
        }
        internal Source(int rate, int channels, byte[] bytes)
        { WaveFormat = new WaveFormat(rate, 16, channels); _chunks = new Queue<byte[]>([bytes]); }
        public WaveFormat WaveFormat { get; }
        public bool CanSeek => false;
        public long Length => throw new NotSupportedException();
        public long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public int Read(byte[] buffer, int offset, int count)
        {
            if (_chunks.Count == 0) return 0;
            var chunk = _chunks.Dequeue(); if (chunk.Length > count) throw new AssertFailedException("Synthetic chunk exceeds capture buffer.");
            chunk.CopyTo(buffer, offset); return chunk.Length;
        }
        public void Dispose() { }
    }
}
