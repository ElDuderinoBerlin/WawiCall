using System.Security.Cryptography;
using CSCore.MediaFoundation;
using WawiCallPro.Audio;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class PersistedMp3NativeTests
{
    [TestMethod]
    public async Task ActualCscoreEncoderCompletesPersistedJobWithDecodableStereoAndOriginalFingerprint()
    {
        var root = Path.Combine(Path.GetTempPath(), "wcp-native-mp3-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new ApplicationPaths(new ConfigurationLocation(root)); Directory.CreateDirectory(paths.RecordingsDirectory);
            var wave = paths.ResolveRecordingPath("native.wav");
            using (var writer = new BinaryWriter(File.Create(wave)))
            {
                writer.Write(0x46464952U); writer.Write(36 + 44100 * 4); writer.Write(0x45564157U); writer.Write(0x20746d66U);
                writer.Write(16); writer.Write((ushort)1); writer.Write((ushort)2); writer.Write(44100); writer.Write(44100 * 4);
                writer.Write((ushort)4); writer.Write((ushort)16); writer.Write(0x61746164U); writer.Write(44100 * 4);
                for (var frame = 0; frame < 44100; frame++)
                { writer.Write((short)(8000 * Math.Sin(frame * 2 * Math.PI * 440 / 44100))); writer.Write((short)(8000 * Math.Sin(frame * 2 * Math.PI * 660 / 44100))); }
            }
            var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(wave))).ToLowerInvariant();
            var target = DatabaseTarget.Sqlite(Path.Combine(root, "native.db")); await new DatabaseProvisioner(new()).CreateAsync(target);
            await new DatabaseMigrator(new(), new FluentMigratorEngine(target.Type)).MigrateAsync(target);
            var context = new DatabaseRepositoryContext(target, new()); var calls = new DapperCallRepository(context);
            var epoch = DateTimeOffset.UtcNow.AddSeconds(-5); var local = Guid.NewGuid();
            var call = new CallSnapshot { Id = Guid.NewGuid(), State = CallState.Connected, Direction = CallDirection.Incoming, Revision = 0,
                StartedAtUtc = epoch, AnsweredAtUtc = epoch, StateChangedAtUtc = epoch, LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Unknown) };
            var saved = await calls.WriteAsync(new(call, null, [], [], new(local, "MP3-NATIVE", "TEST\\MP3")));
            var ended = DateTimeOffset.UtcNow;
            await calls.WriteAsync(new(call with { Revision = 1, State = CallState.Ended, StateChangedAtUtc = ended, EndedAtUtc = ended, EndReason = CallEndReason.Completed },
                saved.Version, [], [], saved.History.Call.Origin!));
            var recording = new Recording(Guid.NewGuid(), call.Id, "native.wav", "wav", null, RecordingAvailability.Pending);
            var captures = new DapperRecordingCaptureRepository(context); var recordingJournal = new DapperRecordingJournal(context);
            await recordingJournal.BeginAsync(new(recording, null, epoch), new(new(recording.Id, call.Id, epoch, null, PcmRecordingState.Capturing, 44100, 2), null, epoch));
            var capture = new RecordingCapture(recording.Id, call.Id, epoch, epoch.AddSeconds(1), PcmRecordingState.Completed, 44100, 2, 44100);
            var changed = DateTimeOffset.UtcNow;
            await recordingJournal.WriteAsync(new(recording with { Availability = RecordingAvailability.Available, Duration = capture.Duration }, 1, changed), new(capture, 1, changed));
            var artifacts = new DapperRecordingArtifactRepository(context);
            var source = new RecordingArtifact(Guid.NewGuid(), recording.Id, call.Id, RecordingArtifactFormat.Wave, "native.wav", RecordingAvailability.Pending);
            await artifacts.WriteAsync(new(source, null, changed));
            await artifacts.WriteAsync(new(new(source.Id, recording.Id, call.Id, source.Format, source.RelativePath, RecordingAvailability.Available, new FileInfo(wave).Length, digest), 1, changed));
            var output = new RecordingArtifact(Guid.NewGuid(), recording.Id, call.Id, RecordingArtifactFormat.Mp3, "native.mp3", RecordingAvailability.Pending);
            var job = new RecordingConversionJob(Guid.NewGuid(), source.Id, output.Id, new(44100, 2, 128000), RecordingConversionState.Pending);
            var jobs = new DapperRecordingConversionRepository(context); var journal = new DapperRecordingConversionJournal(context);
            await journal.BeginAsync(new(output, null, changed), new(job, null, changed));
            var converter = new WavToMp3Converter(new MediaFoundationMp3Backend());
            var secrets = new WindowsUserSecretProtector();
            var store = new ConfigurationStore(new ConfigurationLocation(root), new ApplicationOptionsValidator(secrets), null);
            await store.SaveAsync(new() { Database = new() { SqliteFileName = "native.db" } });
            var configuration = new ApplicationConfigurationService(store); await configuration.StartAsync(CancellationToken.None);
            var outbox = new ProtectedRecordingConversionOutbox(paths, secrets, configuration, new ApplicationDescriptor("WawiCallPro", "Pro"), new LocalRecordingFileProbe(paths));
            var durable = new DurableRecordingConversionJournal(journal, outbox);
            var factory = new PersistedMp3ConversionFactory(jobs, artifacts, captures, calls, new Identity(local), paths, new EmptyOutbox(),
                new LocalRecordingFileProbe(paths), converter, durable, TimeProvider.System, outbox);
            await using var owner = await factory.StartAsync(job.Id);
            var completed = await owner.RunAsync(); var mp3 = paths.ResolveRecordingPath("native.mp3");
            Assert.AreEqual(RecordingConversionState.Completed, completed.Conversion.Job.State); Assert.AreEqual(3L, completed.Conversion.Version);
            Assert.AreEqual(0, outbox.GetPending().Count);
            Assert.AreEqual(2L, completed.Target.Version); Assert.IsTrue(completed.Target.Artifact.ByteLength > 1000);
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mp3))).ToLowerInvariant(), completed.Target.Artifact.Sha256);
            Assert.AreEqual(digest, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(wave))).ToLowerInvariant());
            using var decoder = new MediaFoundationDecoder(mp3); Assert.AreEqual(44100, decoder.WaveFormat.SampleRate); Assert.AreEqual(2, decoder.WaveFormat.Channels);
            var buffer = new byte[decoder.WaveFormat.BytesPerSecond / 10]; long length = 0; var signal = false; int read;
            while ((read = decoder.Read(buffer, 0, buffer.Length)) != 0) { length += read; signal |= buffer.AsSpan(0, read).ContainsAnyExcept((byte)0); }
            Assert.IsTrue(length / (double)decoder.WaveFormat.BytesPerSecond is > 0.9 and < 1.2); Assert.IsTrue(signal);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private sealed class Identity(Guid local) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult<Guid?>(local); public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new InvalidOperationException(); }
    private sealed class EmptyOutbox : IRecordingJournalOutbox
    {
        public IReadOnlyList<Guid> GetPending() => [];
        public IRecordingArtifactAuditLease AcquireInspection(Guid id) => new Lease();
        public Task<IRecordingJournalDelivery> StageAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken token = default) => throw new InvalidOperationException();
        public Task<IRecordingJournalDelivery> OpenAsync(Guid id, CancellationToken token = default) => throw new InvalidOperationException();
        private sealed class Lease : IRecordingArtifactAuditLease
        { public bool HasPending => false; public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }
}
