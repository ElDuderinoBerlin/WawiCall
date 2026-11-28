using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using CSCore;
using CSCore.CoreAudioAPI;

var directory = args[0];
var endpoint = args[1];
using var enumerator = new MMDeviceEnumerator();
using var device = endpoint == "-" ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) : enumerator.GetDevice(endpoint);
using var client = AudioClient.FromMMDevice(device);
var format = client.GetMixFormat();
var encoding = format is WaveFormatExtensible ext
    ? ext.SubFormat.ToString("D")
    : format.WaveFormatTag.ToString();
client.Initialize(AudioClientShareMode.Shared, AudioClientStreamFlags.StreamFlagsLoopback, 2_000_000, 0, format, Guid.Empty);
using var packets = AudioCaptureClient.FromAudioClient(client);
using var output = new StreamWriter(Path.Combine(directory, "loopback.tsv"));
output.WriteLine($"{Stopwatch.Frequency}\t{format.SampleRate}\t{format.Channels}\t{format.BitsPerSample}\t{encoding}");
client.Start();
File.WriteAllText(Path.Combine(directory, "loopback.ready"), Stopwatch.GetTimestamp().ToString());
var timeout = Stopwatch.StartNew();
var answerElapsed = -1L;
var rtp = Path.Combine(directory, "rtp.tsv");
var deadline = -1L;
try
{
    while (timeout.ElapsedMilliseconds < 240000 && !File.Exists(Path.Combine(directory, "loopback.stop")))
    {
        while (packets.GetNextPacketSize() > 0)
        {
            var pointer = packets.GetBuffer(out var frames, out var flags, out var deviceFrame, out var qpc);
            byte[] data;
            try
            {
                data = new byte[checked(frames * format.BlockAlign)];
                if ((flags & AudioClientBufferFlags.Silent) == 0 && pointer != IntPtr.Zero)
                    Marshal.Copy(pointer, data, 0, data.Length);
            }
            finally { packets.ReleaseBuffer(frames); }
            output.WriteLine($"{qpc}\t{Stopwatch.GetTimestamp()}\t{deviceFrame}\t{(int)flags}\t{frames}\t{Convert.ToBase64String(data)}");
        }
        if (timeout.ElapsedMilliseconds >= deadline)
        {
            output.Flush();
            deadline = timeout.ElapsedMilliseconds + 200;
            try
            {
                if (answerElapsed < 0 && File.Exists(rtp) && File.ReadLines(rtp).Any(row => row.StartsWith("answer\t", StringComparison.Ordinal)))
                    answerElapsed = timeout.ElapsedMilliseconds;
            }
            catch (IOException) { }
            if (answerElapsed >= 0 && timeout.ElapsedMilliseconds - answerElapsed > 4000) break;
        }
        Thread.Sleep(3);
    }
}
finally { client.Stop(); }
