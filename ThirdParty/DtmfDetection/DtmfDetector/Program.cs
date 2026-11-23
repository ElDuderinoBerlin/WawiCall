using CSCore;
using CSCore.Codecs.WAV;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using DtmfDetection.CSCore;

namespace DtmfDetector;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return args switch
            {
                ["--file", var path] => AnalyzeFile(path),
                ["--mic"] => Capture(false),
                ["--render"] => Capture(true),
                _ => Usage()
            };
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"DTMF-Analyse fehlgeschlagen ({error.GetType().Name}).");
            return 1;
        }
    }

    private static int Usage()
    {
        Console.WriteLine("DtmfDetector --file <PCM-WAV> | --mic | --render");
        return 2;
    }

    private static int AnalyzeFile(string path)
    {
        using var wave = new WaveFileReader(path);
        foreach (var tone in wave.DtmfTones(forceMono: false)) Console.WriteLine(tone);
        return 0;
    }

    private static int Capture(bool loopback)
    {
        using var finished = new ManualResetEventSlim();
        Exception? failure = null;
        using var device = MMDeviceEnumerator.DefaultAudioEndpoint(loopback ? DataFlow.Render : DataFlow.Capture, Role.Multimedia);
        using WasapiCapture input = loopback ? new WasapiLoopbackCapture() : new WasapiCapture();
        input.Device = device;
        input.Initialize();
        using var analyzer = new LiveAudioDtmfAnalyzer(input);
        analyzer.DtmfToneStopped += tone => Console.WriteLine($"{tone.DtmfTone.Key} ({tone.Channel}) · {tone.Duration.TotalMilliseconds:0} ms");
        analyzer.CaptureFailed += error => { failure = error; finished.Set(); };
        input.Stopped += (_, e) => { if (e.Exception is { } error) failure ??= error; finished.Set(); };
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; finished.Set(); };
        Console.CancelKeyPress += cancel;
        try
        {
            Console.Error.WriteLine("DTMF-Analyse läuft. Strg+C beendet die Aufnahme.");
            analyzer.StartCapturing();
            finished.Wait();
            analyzer.StopCapturing();
            if (failure is { } error) throw new InvalidOperationException("Die Audioanalyse wurde unterbrochen.", error);
            return 0;
        }
        finally { Console.CancelKeyPress -= cancel; }
    }
}
