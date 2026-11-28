using SIPSorcery.SIP.App;
using System.Text.Json;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows.CSCore;

using var settings = JsonDocument.Parse(File.ReadAllText(args[0]));
var id = settings.RootElement.GetProperty("settings").GetProperty("devices").GetProperty("captureDeviceId").GetString();
using var enumerator = new MMDeviceEnumerator();
if (args.Length > 1)
{
    var renderId = settings.RootElement.GetProperty("settings").GetProperty("devices").GetProperty("playbackDeviceId").GetString();
    using var render = renderId is null ? MMDeviceEnumerator.DefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) : enumerator.GetDevice(renderId);
    foreach(var path in args.Skip(1))
    {
        var extension = Path.GetExtension(path);
        try
        {
            using var stream = File.OpenRead(path);
            using CSCore.IWaveSource wave = extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
                ? new CSCore.Codecs.WAV.WaveFileReader(stream)
                : new CSCore.MediaFoundation.MediaFoundationDecoder(stream);
            using var output = new CSCore.SoundOut.WasapiOut(false, AudioClientShareMode.Shared, 110) { Device = render };
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            output.Stopped += (_, e) =>
            {
                if (e.Exception is { } error) stopped.TrySetException(error);
                else stopped.TrySetResult();
            };
            if (wave.CanSeek) wave.Position = Math.Max(0, wave.Length - 2 * wave.WaveFormat.BytesPerSecond);
            output.Initialize(wave);
            var outputClient = (AudioClient)typeof(CSCore.SoundOut.WasapiOut).GetField("_audioClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(output)!;
            var outputFormat = (CSCore.WaveFormat)typeof(CSCore.SoundOut.WasapiOut).GetField("_outputFormat", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(output)!;
            Console.WriteLine($"{extension} output buffer: {outputClient.GetBufferSize() * 1000.0 / outputFormat.SampleRate:F1} ms");
            output.Volume = 0; output.Play();
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(12));
            Console.WriteLine($"{extension} playback completed; {wave.WaveFormat.SampleRate}Hz, {wave.WaveFormat.Channels}ch");
        }
        catch(Exception e)
        {
            Console.WriteLine($"{extension} playback failed {e.GetType().Name} {e.HResult:X8}");
            Console.WriteLine(string.Join(Environment.NewLine, e.StackTrace?.Split('\n').Take(4) ?? []));
        }
    }
    return;
}
using var device = id is null ? MMDeviceEnumerator.DefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia) : enumerator.GetDevice(id);
if (Environment.GetEnvironmentVariable("WAWI_INZONE_RTP_PROBE") == "1")
{
    using var senderCodec = new AudioEncoder();
    using var receiverCodec = new AudioEncoder();
    var format = new AudioFormat(SDPWellKnownMediaFormatsEnum.G722);
    var endpoint = new WindowsAudioEndPoint(senderCodec, null, device, false, true, format, false, 70, 0);
    endpoint.RestrictFormats(candidate => candidate.Codec == AudioCodecsEnum.G722);
    using var sender = new VoIPMediaSession(new MediaEndPoints { AudioSource = endpoint }, System.Net.IPAddress.Loopback, 0);
    using var receiver = new VoIPMediaSession(candidate => candidate.Codec == AudioCodecsEnum.G722);
    int packets = 0, decoded = 0, invalid = 0;
    var gate = new object();
    uint? previous = null;
    receiver.OnRtpPacketReceived += (_, media, packet) =>
    {
        if(media != SDPMediaTypesEnum.audio) return;
        lock(gate)
        {
            packets++;
            if(packet.Payload.Length != 160 || (previous is { } stamp && unchecked(packet.Header.Timestamp - stamp) != 160))
                invalid++;
            previous = packet.Header.Timestamp;
            decoded += receiverCodec.DecodeAudio(packet.Payload, format).Length;
        }
    };
    var address = System.Net.IPAddress.Loopback;
    if (receiver.SetRemoteDescription(SdpType.offer, sender.CreateOffer(address)) != SetDescriptionResultEnum.OK
        || sender.SetRemoteDescription(SdpType.answer, receiver.CreateAnswer(address)) != SetDescriptionResultEnum.OK)
        throw new InvalidOperationException("G.722 negotiation failed.");
    await receiver.Start();
    await sender.Start();
    await Task.Delay(1250);
    sender.Close("Probe ended"); await endpoint.Close();
    if(packets < 30 || invalid > 0 || decoded != packets*320)
        throw new InvalidOperationException($"Local G.722 RTP probe failed: packets={packets}, invalid={invalid}, decoded={decoded}.");
    Console.WriteLine($"Local G.722 RTP: {packets} packets transmitted and decoded; no discontinuity; {decoded} PCM samples.");
    return;
}
using (var client = AudioClient.FromMMDevice(device))
{
    var mix = client.GetMixFormat();
    Console.WriteLine($"Windows input: state={device.DeviceState}, mix={mix.SampleRate}Hz, {mix.Channels}ch, {mix.BitsPerSample}bit, {mix.WaveFormatTag}");
}
try
{
    using var capture = new WasapiCapture(true, AudioClientShareMode.Shared, 100) { Device = device };
    int callbacks = 0, bytes = 0, nonzero = 0;
    capture.DataAvailable += (_, e) =>
    {
        Interlocked.Increment(ref callbacks);
        Interlocked.Add(ref bytes, e.ByteCount);
        if (e.Data.AsSpan(e.Offset, e.ByteCount).ContainsAnyExcept((byte)0)) Interlocked.Increment(ref nonzero);
    };
    capture.Stopped += (_, e) => { if(e.Exception is { } error) Console.WriteLine($"Native stopped {error.GetType().Name} {error.HResult:X8}"); };
    capture.Initialize(); capture.Start();
    await Task.Delay(1200);
    capture.Stop();
    Console.WriteLine($"Native callbacks={callbacks}, bytes={bytes}, nonzero={nonzero}");
}
catch(Exception e) { Console.WriteLine($"Native failed {e.GetType().Name} {e.HResult:X8} {e.StackTrace?.Split('\n').FirstOrDefault()}"); }

foreach (var codec in new[] { SDPWellKnownMediaFormatsEnum.G722, SDPWellKnownMediaFormatsEnum.PCMA })
{
    using var encoder = new AudioEncoder();
    var format = new AudioFormat(codec);
    var probe = new CountingEncoder(encoder);
    var endpoint = new WindowsAudioEndPoint(probe, null, device, false, true, format, allowDeviceFallback: false, captureBufferMs: 70, playbackBufferMs: 110);
    int packets = 0, invalid = 0;
    try
    {
        endpoint.OnAudioSourceEncodedSample += (duration, payload) =>
        {
            Interlocked.Increment(ref packets);
            if (duration == 0 || payload.Length == 0) Interlocked.Increment(ref invalid);
        };
        endpoint.OnAudioSourceError += _ => Console.WriteLine($"{codec} audio source error");
        Console.WriteLine($"{codec} initialized={endpoint.IsAudioSourceInitialized}");
        if (endpoint.IsAudioSourceInitialized)
        {
            var source = (WasapiCapture)typeof(WindowsAudioEndPoint).GetField("_soundInputCapture", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(endpoint)!;
            var captureClient = (AudioClient)typeof(WasapiCapture).GetField("_audioClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(source)!;
            Console.WriteLine($"{codec} capture buffer: {captureClient.GetBufferSize() * 1000.0 / source.WaveFormat.SampleRate:F1} ms");
            endpoint.InputVolume = 1;
            await endpoint.StartAudio();
            await Task.Delay(1300);
            Console.WriteLine($"{codec} running={endpoint.IsAudioSourceRunning}, packets={packets}, bad={invalid}, blocks={probe.Blocks}, samples={probe.Samples}, peak={probe.Peak}, encodingExceptions={probe.Errors}");
        }
    }
    catch(Exception e) { Console.WriteLine($"{codec} failed {e.GetType().Name} {e.HResult:X8} {e.StackTrace?.Split('\n').FirstOrDefault()}"); }
    finally { await endpoint.Close(); }
}

sealed class CountingEncoder(AudioEncoder inner) : IAudioEncoder
{
    public int Blocks, Samples, Peak, Errors;
    public List<AudioFormat> SupportedFormats => inner.SupportedFormats;
    public short[] DecodeAudio(byte[] encoded, AudioFormat format) => inner.DecodeAudio(encoded, format);
    public byte[] EncodeAudio(short[] pcm, AudioFormat format)
    {
        Blocks++; Samples += pcm.Length;
        Peak = Math.Max(Peak, pcm.Select(x => Math.Abs((int)x)).DefaultIfEmpty().Max());
        try { return inner.EncodeAudio(pcm, format); }
        catch { Errors++; throw; }
    }
}
