using SIPSorcery.SIP.App;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using CSCore.CoreAudioAPI;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows.CSCore;

using var list = new MMDeviceEnumerator();
using var endpoints = list.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active);
var devices = endpoints.Where(device => device.FriendlyName.Contains("INZONE", StringComparison.OrdinalIgnoreCase)
    || device.FriendlyName.Contains("Jabra", StringComparison.OrdinalIgnoreCase)).ToArray();
foreach (var device in devices)
{
    using (device)
    {
        Console.WriteLine(device.FriendlyName);
        ProbeNative(device);
        foreach (var buffer in new[] { 0, 70 })
            await ProbeRtpAsync(device, SDPWellKnownMediaFormatsEnum.G722, buffer);
        await ProbeRtpAsync(device, SDPWellKnownMediaFormatsEnum.PCMA, 0);
    }
}
if (devices.Length == 0) throw new InvalidOperationException("No matching active capture endpoints.");

static void ProbeNative(MMDevice device)
{
    using var ready = new AutoResetEvent(false);
    using var client = AudioClient.FromMMDevice(device);
    var format = client.GetMixFormat();
    client.Initialize(AudioClientShareMode.Shared, AudioClientStreamFlags.StreamFlagsEventCallback, 0, 0, format, Guid.Empty);
    client.SetEventHandle(ready);
    using var capture = AudioCaptureClient.FromAudioClient(client);
    var started = Stopwatch.GetTimestamp();
    var ages = new List<double>();
    var framesPerPacket = new List<double>();
    var missing = 0;
    client.Start();
    try
    {
        while (Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(2))
        {
            ready.WaitOne(100);
            while (capture.GetNextPacketSize() > 0)
            {
                capture.GetBuffer(out var frames, out var flags, out _, out var qpc);
                var age = ticks() - qpc;
                capture.ReleaseBuffer(frames);
                if (((int)flags & 4) != 0 || age < 0 || age > TimeSpan.TicksPerSecond) { missing++; continue; }
                if (Stopwatch.GetElapsedTime(started).TotalMilliseconds < 200) continue;
                ages.Add(age / 10000d);
                framesPerPacket.Add(frames * 1000d / format.SampleRate);
            }
        }
    }
    finally { client.Stop(); }
    Console.WriteLine($"  WASAPI {format.SampleRate} Hz, buffer {client.BufferSize * 1000d / format.SampleRate:F1} ms, engine {client.DefaultDevicePeriod / 10000d:F1} ms; n={ages.Count}; first-sample age median/p95 {Percentile(ages, .5):F1}/{Percentile(ages, .95):F1} ms; invalid stamps {missing}; packet {Percentile(framesPerPacket, .5):F1} ms.");
    static long ticks() => (long)((Int128)Stopwatch.GetTimestamp() * TimeSpan.TicksPerSecond / Stopwatch.Frequency);
}

static async Task ProbeRtpAsync(MMDevice device, SDPWellKnownMediaFormatsEnum codec, int buffer)
{
    using var encoder = new AudioEncoder();
    var format = new AudioFormat(codec);
    var endpoint = new WindowsAudioEndPoint(encoder, null, device, false, true, format, false, buffer, 0);
    endpoint.RestrictFormats(candidate => candidate.Codec == format.Codec);
    using var sender = new VoIPMediaSession(new MediaEndPoints { AudioSource = endpoint }, IPAddress.Loopback, 0);
    using var receiver = new VoIPMediaSession(candidate => candidate.Codec == format.Codec);
    var issued = new ConcurrentQueue<long>();
    var arrived = new ConcurrentQueue<(long timestamp, uint rtp, ushort sequence)>();
    endpoint.OnAudioSourceEncodedSample += (_, _) => issued.Enqueue(Stopwatch.GetTimestamp());
    receiver.OnRtpPacketReceived += (_, media, packet) =>
    {
        if (media == SDPMediaTypesEnum.audio) arrived.Enqueue((Stopwatch.GetTimestamp(), packet.Header.Timestamp, packet.Header.SequenceNumber));
    };
    try
    {
        if (receiver.SetRemoteDescription(SdpType.offer, sender.CreateOffer(IPAddress.Loopback)) != SetDescriptionResultEnum.OK
            || sender.SetRemoteDescription(SdpType.answer, receiver.CreateAnswer(IPAddress.Loopback)) != SetDescriptionResultEnum.OK)
            throw new InvalidOperationException("Codec negotiation failed.");
        await receiver.Start();
        var started = Stopwatch.GetTimestamp();
        await sender.Start();
        await Task.Delay(2350);
        sender.Close("probe ended");
        var packets = arrived.ToArray();
        if (packets.Length < 30) throw new InvalidOperationException($"Too few packets: {packets.Length}");
        var origin = packets[0];
        var active = packets.Skip(10).ToArray();
        var arrivalSpacing = active.Zip(active.Skip(1), (a,b) => Stopwatch.GetElapsedTime(a.timestamp,b.timestamp).TotalMilliseconds).ToArray();
        var steps = active.Zip(active.Skip(1), (a,b) => unchecked(b.rtp - a.rtp) * 1000d / format.RtpClockRate).ToArray();
        var jitter = active.Select(x => Stopwatch.GetElapsedTime(origin.timestamp,x.timestamp).TotalMilliseconds -
            unchecked(x.rtp - origin.rtp) * 1000d / format.RtpClockRate).ToArray();
        var lost = packets.Zip(packets.Skip(1), (a,b) => unchecked((ushort)(b.sequence - a.sequence))).Count(diff => diff != 1);
        Console.WriteLine($"  {format.Codec} requested {buffer} ms: {packets.Length} packets, missing sequence {lost}; first RTP {Stopwatch.GetElapsedTime(started, origin.timestamp).TotalMilliseconds:F1} ms; media per packet {Percentile(steps, .5):F1} ms; spacing median/p95/max {Percentile(arrivalSpacing,.5):F1}/{Percentile(arrivalSpacing,.95):F1}/{arrivalSpacing.Max():F1} ms; media-clock arrival spread p05–p95 {Percentile(jitter,.95)-Percentile(jitter,.05):F1} ms.");
    }
    finally { await endpoint.Close(); }
}

static double Percentile(IEnumerable<double> values, double ratio)
{
    var sorted = values.Order().ToArray();
    return sorted.Length == 0 ? double.NaN : sorted[(int)Math.Round(ratio * (sorted.Length - 1))];
}
