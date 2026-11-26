using Azure.Identity;
using Google.Api.Gax.Grpc;
using Google.Cloud.TextToSpeech.V1;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Audio;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class CloudAnnouncementSpeechTests
{
    [TestMethod]
    public async Task BothProvidersResolveByKeyAndGoogleWritesTheConfiguredWave()
    {
        var audio = TestWave();
        var client = new GoogleSpeechDouble((_, _) => Task.FromResult(audio));
        using var services = CreateServices(client);
        var azure = services.GetRequiredKeyedService<IAnnouncementSpeech>(CloudAnnouncementSpeechServices.AzureSpeech);
        var google = services.GetRequiredKeyedService<IAnnouncementSpeech>(CloudAnnouncementSpeechServices.GoogleCloud);
        CollectionAssert.AreEqual(new[] { "de-DE-KatjaNeural", "de-DE-ConradNeural" }, azure.GetVoices().Select(voice => voice.Id).ToArray());
        Assert.AreNotSame(azure, google);
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            const string text = "Für Frau Özdemir drücken Sie die 2.";
            var path = Path.Combine(folder.FullName, "ansage.wav");
            var voice = await google.WriteWaveAsync(text, "de-DE-Chirp3-HD-Achernar", path, default);
            Assert.AreEqual(voice.Id, client.LastRequest!.Voice.Name);
            Assert.AreEqual("de-DE", client.LastRequest.Voice.LanguageCode);
            Assert.AreEqual(text, client.LastRequest.Input.Text);
            Assert.AreEqual(AudioEncoding.Linear16, client.LastRequest.AudioConfig.AudioEncoding);
            Assert.AreEqual(16000, client.LastRequest.AudioConfig.SampleRateHertz);
            CollectionAssert.AreEqual(audio, await File.ReadAllBytesAsync(path));
            await Assert.ThrowsAsync<AnnouncementSpeechException>(() => google.WriteWaveAsync(text, null, path, default));
            Assert.AreEqual(1, client.Calls);
        }
        finally { folder.Delete(recursive: true); }
    }

    [TestMethod]
    public async Task InvalidOrCancelledSynthesisLeavesNoFiles()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new GoogleSpeechDouble(async (request, token) =>
        {
            if (request.Input.Text == "Ungültig") return new byte[] { 1, 2, 3 };
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return TestWave();
        });
        using var services = CreateServices(client);
        var google = services.GetRequiredKeyedService<IAnnouncementSpeech>(CloudAnnouncementSpeechServices.GoogleCloud);
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(folder.FullName, "ansage.wav");
            await Assert.ThrowsAsync<AnnouncementSpeechException>(() => google.WriteWaveAsync("Ungültig", null, path, default));
            using var cancellation = new CancellationTokenSource();
            var pending = google.WriteWaveAsync("Abbrechen", null, path, cancellation.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
            Assert.IsEmpty(folder.GetFiles());
        }
        finally { folder.Delete(recursive: true); }
    }

    [TestMethod]
    public async Task GoogleVoiceCatalogAddsOnlyGermanVoicesWithSupportedBudgets()
    {
        var client = new GoogleSpeechDouble((_, _) => Task.FromResult(TestWave()));
        using var services = CreateServices(client);
        var google = services.GetRequiredKeyedService<IAnnouncementSpeech>(CloudAnnouncementSpeechServices.GoogleCloud);
        var voices = await google.LoadVoicesAsync(default);
        Assert.AreEqual("de-DE", client.ListLocale);
        CollectionAssert.AreEquivalent(new[] { "de-DE-Neural2-G", "de-DE-Chirp3-HD-Achernar", "de-DE-Chirp3-HD-Puck" },
            voices.Select(voice => voice.Id).ToArray());
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            await google.WriteWaveAsync("Guten Tag.", "de-DE-Chirp3-HD-Puck", Path.Combine(folder.FullName, "puck.wav"), default);
            Assert.AreEqual("de-DE-Chirp3-HD-Puck", client.LastRequest!.Voice.Name);
        }
        finally { folder.Delete(recursive: true); }
    }

    private static ServiceProvider CreateServices(TextToSpeechClient google)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton(CloudAnnouncementSpeechServices.GoogleCloud, google);
        services.AddAudioConversion()
            .AddAzureAnnouncementSpeech(new Uri("https://speech-example.cognitiveservices.azure.com/"), new AzureCliCredential())
            .AddGoogleAnnouncementSpeech("tts-test-project");
        var unkeyed = services.Single(service => service.ServiceType == typeof(IAnnouncementSpeech) && !service.IsKeyedService);
        Assert.AreEqual(typeof(WindowsAnnouncementSpeech), unkeyed.ImplementationType);
        return services.BuildServiceProvider();
    }

    private static byte[] TestWave()
    {
        using var stream = new MemoryStream();
        using (var writer = new CSCore.Codecs.WAV.WaveWriter(stream, new CSCore.WaveFormat(16000, 16, 1)))
            writer.Write(new byte[320], 0, 320);
        return stream.ToArray();
    }

    private sealed class GoogleSpeechDouble(Func<SynthesizeSpeechRequest, CancellationToken, Task<byte[]>> synthesize) : TextToSpeechClient
    {
        internal SynthesizeSpeechRequest? LastRequest { get; private set; }
        internal int Calls { get; private set; }
        internal string? ListLocale { get; private set; }

        public override Task<ListVoicesResponse> ListVoicesAsync(ListVoicesRequest request, CallSettings callSettings)
        {
            ListLocale = request.LanguageCode;
            return Task.FromResult(new ListVoicesResponse
            {
                Voices =
                {
                    new Voice { Name = "de-DE-Chirp3-HD-Puck", LanguageCodes = { "de-DE" } },
                    new Voice { Name = "de-DE-Standard-A", LanguageCodes = { "de-DE" } },
                    new Voice { Name = "en-GB-Chirp3-HD-Achernar", LanguageCodes = { "en-GB" } }
                }
            });
        }

        public override async Task<SynthesizeSpeechResponse> SynthesizeSpeechAsync(SynthesizeSpeechRequest request, CallSettings callSettings)
        {
            LastRequest = request;
            Calls++;
            var bytes = await synthesize(request, callSettings?.CancellationToken ?? default);
            return new SynthesizeSpeechResponse { AudioContent = ByteString.CopyFrom(bytes) };
        }
    }
}
