using CSCore;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.Audio;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public async Task NativeHistoryTransportPausesAndSeeksWithSilentWasapiOutput()
    {
        if (Environment.GetEnvironmentVariable("WCP_VERIFY_RECORDING_OUTPUT_DEVICE") != "1")
            Assert.Inconclusive("Requires a Windows playback endpoint.");

        const int rate = 16000;
        var duration = TimeSpan.FromSeconds(6);
        using var source = new CSCore.Codecs.RAW.RawDataReader(new MemoryStream(new byte[6 * rate * 2]), new WaveFormat(rate, 16, 1));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var transport = new HistoryPlaybackTransport();
        transport.SetMedia(new(RecordingArtifactFormat.Wave, rate, 1, 16, duration, true, null));
        var output = new WasapiHistoryRecordingOutput();
        var playing = Task.Run(() => output.Play(source, new AudioDeviceSettings { PlaybackVolumePercent = 0 }, transport, timeout.Token));
        try
        {
            await WaitForAsync(() => transport.Position >= TimeSpan.FromMilliseconds(250));
            transport.Paused = true;
            await Task.Delay(250, timeout.Token);
            var pausedAt = transport.Position;
            await Task.Delay(200, timeout.Token);
            Assert.AreEqual(pausedAt, transport.Position);

            var seekTo = TimeSpan.FromSeconds(4);
            transport.Seek(seekTo);
            await WaitForAsync(() => transport.Position >= seekTo);
            Assert.AreEqual(seekTo, transport.Position);
            transport.Paused = false;
            await playing.WaitAsync(timeout.Token);
            Assert.AreEqual(duration, transport.Position);
        }
        finally
        {
            await timeout.CancelAsync();
            try { await playing; }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
        }

        async Task WaitForAsync(Func<bool> condition)
        {
            while (!condition())
            {
                if (playing.IsCompleted)
                {
                    await playing;
                    Assert.Fail("The playback endpoint stopped before the requested position was reached.");
                }
                await Task.Delay(25, timeout.Token);
            }
        }
    }

    [TestMethod]
    [DataRow(8000, 2)]
    [DataRow(16000, 1)]
    [DataRow(48000, 2)]
    public Task RecordingOutputConvertsDecodesAndExportsThroughHistory(int sampleRate, int channels) => WithWorkspace(async (workspace, services, _) =>
    {
        await workspace.Database.CreateAndMigrateAsync();
        var workstation = services.GetRequiredService<IWorkstationIdentity>();
        var local = await workstation.InitializeAsync();
        var context = services.GetRequiredService<DatabaseRepositoryContext>();
        var calls = services.GetRequiredService<ICallRepository>();
        var artifacts = services.GetRequiredService<IRecordingArtifactRepository>();
        var captures = services.GetRequiredService<IRecordingCaptureRepository>();
        var jobs = services.GetRequiredService<IRecordingConversionRepository>();
        var journal = services.GetRequiredService<IRecordingJournal>();
        var pending = services.GetRequiredService<IRecordingJournalOutbox>();
        var conversions = services.GetRequiredService<IRecordingConversionOutbox>();
        var inspector = services.GetRequiredService<IRecordingArtifactFileInspector>();
        var exporter = services.GetRequiredService<IRecordingExport>();
        var configuration = services.GetRequiredService<ApplicationConfigurationService>();
        var catalog = new RecordingPlaybackCatalog(context);
        var nativeOutput = Environment.GetEnvironmentVariable("WCP_VERIFY_RECORDING_OUTPUT_DEVICE") == "1"
            ? services.GetRequiredService<IHistoryRecordingOutput>() : null;
        var output = new RecordingPcmOutput(nativeOutput);
        var playback = new HistoryRecordingPlayback(catalog, workstation, pending, conversions, jobs, inspector,
            configuration, workspace.Settings, output);
        var requests = new RecordingConversionRequests(artifacts, captures, jobs, services.GetRequiredService<IRecordingConversionJournal>(),
            calls, workstation, pending, conversions, inspector, services.GetRequiredService<IRecordingFileProbe>(),
            new RecordingConversionSignals(), TimeProvider.System);
        var formats = services.GetRequiredService<IRecordingFormatConverters>();
        var files = new HistoryRecordingFiles(catalog, workstation, artifacts, captures, jobs, requests, exporter,
            formats.GetConverter(RecordingArtifactFormat.Mp3));
        var automatic = new AutomaticRecordingConversions(workspace.Settings, configuration, workstation,
            services.GetRequiredService<IRecordingArtifactAuditQuery>(), jobs, captures, requests, formats, artifacts);
        var batch = new RecordingConversionBatch(jobs, workstation, services.GetRequiredService<IPersistedMp3ConversionFactory>(), automatic);
        var origin = new CallOrigin(local, "TEST", "TEST\\RecordingFormat");
        Directory.CreateDirectory(workspace.Paths.RecordingsDirectory);

        foreach (var format in Enum.GetValues<RecordingArtifactFormat>())
        {
            var snapshot = await workspace.Settings.LoadAsync();
            var now = DateTimeOffset.UtcNow.AddMinutes(-1).AddSeconds(4 * (int)format);
            await workspace.Settings.SaveAsync(snapshot.Options with
            {
                RecordingOutput = new(format, format == RecordingArtifactFormat.Wave ? null : now)
            }, snapshot);

            var (callId, recordingId) = await RecordSyntheticCallAsync(calls, journal, origin, now, workspace.Paths.RecordingsDirectory, sampleRate, channels);
            var result = await batch.RunAsync();
            var expected = new RecordingConversionBatchResult(format == RecordingArtifactFormat.Wave ? 0 : 1, 0, 0);
            if (result != expected)
            {
                var failures = (await jobs.GetPageAsync(local)).Items.Select(item => $"{item.Job.Format}: {item.Job.State}/{item.Job.Failure}");
                Assert.AreEqual(expected, result, $"{format}. Jobs: {string.Join(", ", failures)}");
            }
            var created = (await artifacts.GetForRecordingAsync(recordingId)).Single(item => item.Artifact.Format == format);
            Assert.AreEqual(RecordingAvailability.Available, created.Artifact.Availability);
            Assert.AreEqual(new RecordingConversionBatchResult(0, 0, 0), await batch.RunAsync(), "Conversion is idempotent.");

            Assert.AreEqual(format, (await catalog.FindAsync(local, [callId], preferredFormat: format))[callId].Format);
            Assert.IsTrue((await playback.FindAsync([callId], default)).Contains(callId));
            output.Reset();
            var transport = new HistoryPlaybackTransport();
            await playback.PlayAsync(callId, transport, default);
            Assert.IsNotNull(transport.Media);
            Assert.AreEqual(format, transport.Media.Format);
            Assert.AreEqual(output.SampleRate, transport.Media.SampleRate);
            Assert.AreEqual(channels, transport.Media.Channels);
            Assert.AreEqual(workspace.Paths.ResolveRecordingPath(created.Artifact.RelativePath), transport.Media.FilePath);
            Assert.IsTrue(transport.Media.Duration > TimeSpan.FromMilliseconds(500));
            Assert.AreEqual(channels, output.Channels);
            Assert.IsTrue(output.Frames >= output.SampleRate / 2, $"Decoded frames for {format}: {output.Frames}");
            Assert.IsTrue(output.ContainsAudio, $"Decoded PCM was silent for {format}.");

            var choices = await files.GetExportChoicesAsync(callId, default);
            Assert.AreEqual(format == RecordingArtifactFormat.Wave ? 1 : 2, choices.Count);
            var choice = choices.Single(item => item.Format == format);
            var destination = Path.Combine(Path.GetTempPath(), $"wcp-export-{recordingId:N}.{format.Extension()}");
            try
            {
                await files.ExportAsync(callId, choice, destination, default);
                CollectionAssert.AreEqual(await File.ReadAllBytesAsync(workspace.Paths.ResolveRecordingPath(created.Artifact.RelativePath)),
                    await File.ReadAllBytesAsync(destination));
            }
            finally { if (File.Exists(destination)) File.Delete(destination); }
        }
    }, configure: services =>
    {
        services.AddAudioConversion();
        services.AddSingleton<IPersistedMp3ConversionFactory, PersistedMp3ConversionFactory>();
    });

    private static async Task<(Guid Call, Guid Recording)> RecordSyntheticCallAsync(ICallRepository calls, IRecordingJournal journal,
        CallOrigin origin, DateTimeOffset now, string recordingDirectory, int rate, int channels)
    {
        var call = new CallSnapshot
        {
            Id = Guid.NewGuid(), Direction = CallDirection.Incoming, Revision = 0, State = CallState.Connected,
            StartedAtUtc = now, AnsweredAtUtc = now, StateChangedAtUtc = now,
            LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Unknown)
        };
        var stored = await calls.WriteAsync(new(call, null, [], [], origin));
        var recording = new Recording(Guid.NewGuid(), call.Id, $"{call.Id:N}.wav", "wav", null, RecordingAvailability.Pending);
        var frames = rate;
        var blockAlign = channels * sizeof(short);
        var dataBytes = frames * blockAlign;
        await journal.BeginAsync(new(recording, null, now),
            new(new(recording.Id, call.Id, now, null, PcmRecordingState.Capturing, rate, channels), null, now));
        using (var writer = new BinaryWriter(File.Create(Path.Combine(recordingDirectory, recording.RelativePath))))
        {
            writer.Write("RIFF"u8); writer.Write(36 + dataBytes); writer.Write("WAVEfmt "u8); writer.Write(16);
            writer.Write((ushort)1); writer.Write((ushort)channels); writer.Write(rate); writer.Write(rate * blockAlign);
            writer.Write((ushort)blockAlign); writer.Write((ushort)16); writer.Write("data"u8); writer.Write(dataBytes);
            for (var frame = 0; frame < frames; frame++)
            {
                writer.Write((short)(12000 * Math.Sin(2 * Math.PI * 440 * frame / rate)));
                if (channels == 2) writer.Write((short)(9000 * Math.Sin(2 * Math.PI * 660 * frame / rate)));
            }
        }
        var end = now.AddSeconds(1);
        var capture = new RecordingCapture(recording.Id, call.Id, now, end, PcmRecordingState.Completed, rate, channels, frames);
        await journal.WriteAsync(new(recording with { Availability = RecordingAvailability.Available, Duration = capture.Duration }, 1, end),
            new(capture, 1, end));
        await calls.WriteAsync(new(stored.History.Call with { State = CallState.Ended, Revision = 1, StateChangedAtUtc = end,
            EndedAtUtc = end, EndReason = CallEndReason.Completed }, stored.Version, [], [], origin));
        return (call.Id, recording.Id);
    }

    private sealed class RecordingPcmOutput(IHistoryRecordingOutput? device) : IHistoryRecordingOutput
    {
        public int Frames { get; private set; }
        public int Channels { get; private set; }
        public int SampleRate { get; private set; }
        public bool ContainsAudio { get; private set; }

        internal void Reset()
        {
            Frames = 0;
            ContainsAudio = false;
        }

        public void Play(IWaveSource source, AudioDeviceSettings settings, CancellationToken token)
        {
            Channels = source.WaveFormat.Channels;
            SampleRate = source.WaveFormat.SampleRate;
            if (device is not null)
            {
                device.Play(source, settings with { PlaybackVolumePercent = 0 }, token);
                source.Position = 0;
            }
            var buffer = new byte[4096];
            int count;
            while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                Frames += count / source.WaveFormat.BlockAlign;
                ContainsAudio |= buffer.AsSpan(0, count).ContainsAnyExcept((byte)0);
            }
        }
    }
}
