using System.Net;
using System.Net.Sockets;
using System.Text;
using CSCore.Codecs.MP3;

using var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
var port = ((IPEndPoint)listener.LocalEndpoint).Port;
var data = File.ReadAllBytes(args[0]);
var connected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
var server = Task.Run(async () =>
{
    using var client = await listener.AcceptTcpClientAsync(stop.Token);
    using var stream = client.GetStream();
    using var reader = new StreamReader(stream, leaveOpen: true);
    while (await reader.ReadLineAsync(stop.Token) is { Length: > 0 }) { }

    await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: audio/mpeg\r\nConnection: close\r\n\r\n"), stop.Token);
    await stream.WriteAsync(data, stop.Token);
    await stream.FlushAsync(stop.Token);
    await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token);
});

using (var playback = new Mp3WebStream(new Uri($"http://127.0.0.1:{port}/stream"), true))
{
    playback.ConnectionEstablished += (_, e) => connected.TrySetResult(e.Success);
    if (!await connected.Task.WaitAsync(stop.Token))
        throw new InvalidOperationException("MP3 stream did not initialize.");

    var buffer = new byte[4096];
    var deadline = DateTime.UtcNow.AddSeconds(4);
    var decoded = false;
    while (DateTime.UtcNow < deadline && !decoded)
    {
        playback.Read(buffer, 0, buffer.Length);
        decoded = buffer.Any(value => value != 0);
        if (!decoded)
            await Task.Delay(20, stop.Token);
    }
    if (!decoded)
        throw new InvalidOperationException("No decoded PCM reached the player.");
}

await stop.CancelAsync();
try { await server; }
catch (OperationCanceledException) { }
Console.WriteLine("Local MP3 decoded to nonzero PCM and disposal completed with the source still connected.");
