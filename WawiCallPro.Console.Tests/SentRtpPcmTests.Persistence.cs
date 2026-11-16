using System.Buffers.Binary;
using CSCore.Codecs.WAV;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Audio;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Console.Tests;

public sealed partial class SentRtpPcmTests
{
    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task ActualDuplexCaptureAndWaveCompletionPersistTogetherInSqlite(SDPWellKnownMediaFormatsEnum codec)
    {
        await using var fixture = new Fixture(codec); fixture.EnableReceiveBuffer(); await fixture.Connect(); using var files = new PersistedFiles();
        var request = await files.Initialize(fixture); await using var media = files.Media(fixture);
        var recording = await media.StartRecordingAsync(request); var rate = fixture.Format.ClockRate; var block = rate / 50;
        Assert.AreEqual(RecordingAvailability.Pending, (await files.Recording())!.Recording.Availability);
        var input = Samples(block, 0); fixture.Capture(input); await fixture.Packets(1);
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Media.OnRtpPacketReceived += (_, type, _) => { if (type == SDPMediaTypesEnum.audio) received.TrySetResult(); };
        var payload = new AudioEncoder().EncodeAudio(Samples(block, 1), fixture.Format); var expected = new AudioEncoder().DecodeAudio(payload, fixture.Format);
        fixture.SendRemote(100, 5000, payload); await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await recording.StopAsync(TimeSpan.FromMilliseconds(40)); var saved = (await files.Capture())!;
        Assert.AreEqual(PcmRecordingState.Completed, saved.Capture.State); Assert.AreEqual(block * 2L, saved.Capture.FrameCount);
        Assert.AreEqual(request.RecordingEpochUtc!.Value.AddMilliseconds(40), saved.Capture.EndedAtUtc);
        var row = (await files.Recording())!; Assert.AreEqual(RecordingAvailability.Available, row.Recording.Availability);
        Assert.AreEqual("captured.wav", row.Recording.RelativePath); Assert.AreEqual(TimeSpan.FromMilliseconds(40), row.Recording.Duration);
        using var reader = new WaveFileReader(files.Output); var bytes = new byte[block * 2 * 4]; Assert.AreEqual(bytes.Length, reader.Read(bytes, 0, bytes.Length));
        for (var frame = 0; frame < block * 2; frame++)
        {
            Assert.AreEqual(frame < block ? input[frame] : (short)0, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(frame * 4, 2)));
            var offset = frame - rate / 100;
            Assert.AreEqual(offset >= 0 && offset < expected.Length ? expected[offset] : (short)0, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(frame * 4 + 2, 2)));
        }
        await media.DisposeAsync(); Assert.AreEqual(saved, await files.Capture());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RealWaveSurvivesFailedDatabaseConfirmationAndAnExactPersistenceRetry(bool commitUncertain)
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        var faults = new FaultJournal(files.Journal, commitUncertain); var media = files.Media(fixture, faults);
        try
        {
            var recording = await media.StartRecordingAsync(request); fixture.Capture(Samples(160, 0)); await fixture.Packets(1);
            var error = await Assert.ThrowsExactlyAsync<RecordingPersistenceException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(40)));
            Assert.IsTrue(error.FinalizedWavePreserved); var before = File.ReadAllBytes(files.Output); Assert.IsTrue(before.Length > 44);
            Assert.AreEqual(commitUncertain ? RecordingAvailability.Available : RecordingAvailability.Pending, (await files.Recording())!.Recording.Availability);
            var status = (IRecordingPersistenceStatus)recording; Assert.IsTrue(status.HasPendingPersistence); await status.RetryPersistenceAsync();
            Assert.IsFalse(status.HasPendingPersistence); Assert.AreEqual(2L, (await files.Recording())!.Version);
            Assert.AreEqual(2L, (await files.Capture())!.Version); CollectionAssert.AreEqual(before, File.ReadAllBytes(files.Output));
            Assert.AreEqual(1, fixture.Codec.Encodes);
            var next = request with { Recording = request.Recording with { RecordingId = Guid.NewGuid(), WavePath = Path.Combine(files.Paths.RecordingsDirectory, "next.wav") } };
            var resumed = await media.StartRecordingAsync(next); Assert.AreEqual(PcmRecordingState.Capturing, resumed.State);
            await resumed.DisposeAsync();
        }
        finally { try { await media.DisposeAsync(); } catch (RecordingPersistenceException) { } }
    }

    [TestMethod]
    public async Task CallOwnedMediaAbortPersistsCancellationWithoutAnEndOrPublishedWave()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        await using var media = files.Media(fixture); var recording = await media.StartRecordingAsync(request);
        fixture.Capture(Samples(160, 0)); await fixture.Packets(1); await media.DisposeAsync();
        Assert.AreEqual(PcmRecordingState.Aborted, recording.State); Assert.IsFalse(File.Exists(files.Output));
        var capture = (await files.Capture())!.Capture; Assert.AreEqual(RecordingCaptureFailure.Cancelled, capture.Failure);
        Assert.IsNull(capture.EndedAtUtc); Assert.IsNull(capture.Duration); Assert.AreEqual(RecordingAvailability.Failed, (await files.Recording())!.Recording.Availability);
    }

    [TestMethod]
    public async Task StartupFileConflictIsPersistedWithoutReplacingTheExistingFile()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        Directory.CreateDirectory(Path.GetDirectoryName(files.Output)!); File.WriteAllBytes(files.Output, [11, 22, 33]);
        await using var media = files.Media(fixture);
        await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => media.StartRecordingAsync(request));
        CollectionAssert.AreEqual(new byte[] { 11, 22, 33 }, File.ReadAllBytes(files.Output));
        Assert.AreEqual(PcmRecordingState.Failed, (await files.Capture())!.Capture.State);
        Assert.AreEqual(RecordingAvailability.Failed, (await files.Recording())!.Recording.Availability);
    }

    private sealed class PersistedFiles : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "wcp-persistent-wave-" + Guid.NewGuid().ToString("N"));
        private DatabaseTarget _target = null!; private Guid _id;
        internal ApplicationPaths Paths { get; }
        internal DatabaseTarget Target => _target;
        internal string Output => Path.Combine(Paths.RecordingsDirectory, "captured.wav");
        internal IRecordingJournal Journal { get; private set; } = null!;
        internal PersistedFiles() => Paths = new(new ConfigurationLocation(_root));
        internal async Task<DuplexPcmRecordingRequest> Initialize(Fixture fixture)
        {
            _target = DatabaseTarget.Sqlite(Path.Combine(_root, "communications.db")); await new DatabaseProvisioner(new()).CreateAsync(_target);
            Journal = new DapperRecordingJournal(new(_target, new()));
            await new DatabaseMigrator(new(), new FluentMigratorEngine(_target.Type)).MigrateAsync(_target);
            var epoch = DateTimeOffset.UtcNow.AddMinutes(-1); var call = new CallSnapshot { Id = Guid.NewGuid(), Revision = 0, State = CallState.Connected,
                Direction = CallDirection.Incoming, LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Unknown), StartedAtUtc = epoch, AnsweredAtUtc = epoch, StateChangedAtUtc = epoch };
            await new DapperCallRepository(new(_target, new())).WriteAsync(new(call, null, [], [], new(Guid.NewGuid(), "PCM-TEST", "TEST\\PCM")));
            _id = Guid.NewGuid(); return new(new(call.Id, _id, Output, fixture.Format.ClockRate), Anchor(fixture),
                new(fixture.RemoteSsrc, 100, 5000, 8000, TimeSpan.FromMilliseconds(10))) { RecordingEpochUtc = epoch };
        }
        internal RecordingSipMediaSession Media(Fixture fixture, IRecordingJournal? journal = null) => new(
            new PcmSipMediaSession(new RecordingNativeMedia(fixture.Media), fixture.Tap, fixture.Bridge), new PersistedDuplexPcmRecordingFactory(DuplexFactory(), journal ?? Journal, Paths, TimeProvider.System));
        internal Task<StoredRecording?> Recording() => new DapperRecordingRepository(new(_target, new())).GetAsync(_id);
        internal Task<StoredRecordingCapture?> Capture() => new DapperRecordingCaptureRepository(new(_target, new())).GetAsync(_id);
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }
    private sealed class FaultJournal(IRecordingJournal inner, bool commit) : IRecordingJournal
    {
        private bool _failed;
        public Task<StoredRecordingJournal> BeginAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default) => inner.BeginAsync(recording, capture, cancellationToken);
        public async Task<StoredRecordingJournal> WriteAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default)
        {
            if (_failed) return await inner.WriteAsync(recording, capture, cancellationToken);
            _failed = true; if (commit) await inner.WriteAsync(recording, capture, cancellationToken);
            throw new DatabaseOperationException(DatabaseErrorCode.CommitUncertain);
        }
    }
}
