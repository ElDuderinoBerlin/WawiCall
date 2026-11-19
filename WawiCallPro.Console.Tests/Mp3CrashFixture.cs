using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
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

internal sealed record Mp3CrashSeed(Guid JobId, Guid SourceId, Guid TargetId, Guid RecordingId, Guid WorkstationId, string WaveSha256);
internal sealed record Mp3CrashSignal(string Point, int ProcessId, string AssemblyPath, string AudioAssemblyPath,
    Mp3CrashSeed Seed, string RelativePath, long Length, string Sha256)
{ public string Engine { get; init; } = "SQLite"; }

/// <summary>Owns only synthetic temporary files. It deliberately has no product-host startup or audio devices.</summary>
internal sealed class Mp3CrashFixture
{
#if LX_FINOFFICE
    internal const string ProductLine = "FinOffice";
#else
    internal const string ProductLine = "Pro";
#endif
    internal ApplicationPaths Paths { get; }
    internal DapperRecordingConversionRepository Jobs { get; }
    internal DapperRecordingConversionJournal Journal { get; }
    internal DapperRecordingArtifactRepository Artifacts { get; }
    internal DapperRecordingRepository Recordings { get; }
    internal DapperRecordingCaptureRepository Captures { get; }
    internal DapperCallRepository Calls { get; }
    internal IRecordingJournalOutbox RecordingLeases { get; }
    internal IRecordingConversionOutbox Conversions { get; }
    internal string Engine { get; }
    private readonly DatabaseRepositoryContext _context;
    internal string Wave => Paths.ResolveRecordingPath("process.wav");
    internal string Output => Paths.ResolveRecordingPath("process.mp3");
    private Mp3CrashFixture(string root, ApplicationConfigurationService configuration)
    {
        Paths = new(new ConfigurationLocation(root));
        var target = new ConfiguredDatabaseTarget(configuration, Paths, new WindowsUserSecretProtector()).GetTarget();
        if (target.Type != DatabaseType.Sqlite) ValidateServerTarget(target);
        _context = new(target, new());
        Engine = File.Exists(Path.Combine(root, "engine.txt")) ? File.ReadAllText(Path.Combine(root, "engine.txt")) : "SQLite";
        if (Engine is not ("SQLite" or "SQL Server" or "MySQL" or "MariaDB" or "PostgreSQL")) throw new AssertFailedException("Invalid fixture engine.");
        Jobs = new(_context); Journal = new(_context); Artifacts = new(_context); Recordings = new(_context); Captures = new(_context); Calls = new(_context);
        var secrets = new WindowsUserSecretProtector(); var application = new ApplicationDescriptor("WawiCallPro", ProductLine);
        RecordingLeases = new ProtectedRecordingJournalOutbox(Paths, secrets, configuration, application);
        Conversions = new ProtectedRecordingConversionOutbox(Paths, secrets, configuration, application, new LocalRecordingFileProbe(Paths));
    }
    internal static void ValidateRoot(string root)
    {
        var full = Path.GetFullPath(root); var temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var name = Path.GetFileName(full);
        if (!string.Equals(Path.GetDirectoryName(full), temporary, StringComparison.OrdinalIgnoreCase) ||
            !name.StartsWith("wcp-mp3-process-", StringComparison.Ordinal) || !Guid.TryParseExact(name[16..], "N", out _) ||
            (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new AssertFailedException("Invalid isolated fixture root.");
    }
    internal static async Task<Mp3CrashFixture> OpenAsync(string root)
    {
        ValidateRoot(root);
        var secrets = new WindowsUserSecretProtector(); var store = new ConfigurationStore(new(root), new ApplicationOptionsValidator(secrets), null);
        var configuration = new ApplicationConfigurationService(store); await configuration.StartAsync(CancellationToken.None);
        return new(root, configuration);
    }
    internal RecordingConversionRecovery Recovery(Guid local) => new(Conversions, Journal, Jobs, Artifacts, Calls, RecordingLeases,
        new Identity(local), NullLogger<RecordingConversionRecovery>.Instance);
    internal InterruptedMp3Recovery Interrupted(Guid local) => new(new DapperInterruptedRecordingConversionQuery(_context), Jobs, Artifacts, Calls,
        RecordingLeases, Conversions, new DurableRecordingConversionJournal(Journal, Conversions), new LocalRecordingFileProbe(Paths),
        new Identity(local), TimeProvider.System, NullLogger<InterruptedMp3Recovery>.Instance);

    internal static async Task RunChildAsync(string root, string point)
    {
        if (!File.Exists(new ConfigurationLocation(root).FilePath))
        {
            var target = DatabaseTarget.Sqlite(Path.Combine(root, "crash.db"));
            var creation = await new DatabaseProvisioner(new()).CreateAsync(target);
            await new DatabaseMigrator(new(), new FluentMigratorEngine(target.Type)).MigrateAsync(target, creation: creation);
            var secrets = new WindowsUserSecretProtector();
            await new ConfigurationStore(new(root), new ApplicationOptionsValidator(secrets), null).SaveAsync(new() { Database = new() { SqliteFileName = "crash.db" } });
        }
        var fixture = await OpenAsync(root); Directory.CreateDirectory(fixture.Paths.RecordingsDirectory);
        using (var writer = new BinaryWriter(File.Create(fixture.Wave)))
        {
            writer.Write(0x46464952U); writer.Write(36 + 44100 * 4); writer.Write(0x45564157U); writer.Write(0x20746d66U);
            writer.Write(16); writer.Write((ushort)1); writer.Write((ushort)2); writer.Write(44100); writer.Write(44100 * 4);
            writer.Write((ushort)4); writer.Write((ushort)16); writer.Write(0x61746164U); writer.Write(44100 * 4);
            for (var frame = 0; frame < 44100; frame++)
            { writer.Write((short)(8000 * Math.Sin(frame * 2 * Math.PI * 440 / 44100))); writer.Write((short)(8000 * Math.Sin(frame * 2 * Math.PI * 660 / 44100))); }
        }
        File.WriteAllBytes(Path.Combine(fixture.Paths.RecordingsDirectory, "unrelated.tmp"), [17, 29, 41]);
        var epoch = DateTimeOffset.UtcNow.AddSeconds(-5); var local = Guid.NewGuid();
        var call = new CallSnapshot { Id = Guid.NewGuid(), State = CallState.Connected, Direction = CallDirection.Incoming, Revision = 0,
            StartedAtUtc = epoch, AnsweredAtUtc = epoch, StateChangedAtUtc = epoch, LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Unknown) };
        var saved = await fixture.Calls.WriteAsync(new(call, null, [], [], new(local, "MP3-CRASH", "TEST\\MP3")));
        var changed = DateTimeOffset.UtcNow;
        await fixture.Calls.WriteAsync(new(call with { Revision = 1, State = CallState.Ended, StateChangedAtUtc = changed, EndedAtUtc = changed, EndReason = CallEndReason.Completed },
            saved.Version, [], [], saved.History.Call.Origin!));
        var recording = new Recording(Guid.NewGuid(), call.Id, "process.wav", "wav", null, RecordingAvailability.Pending);
        var recordingJournal = new DapperRecordingJournal(fixture._context);
        await recordingJournal.BeginAsync(new(recording, null, epoch), new(new(recording.Id, call.Id, epoch, null, PcmRecordingState.Capturing, 44100, 2), null, epoch));
        var capture = new RecordingCapture(recording.Id, call.Id, epoch, epoch.AddSeconds(1), PcmRecordingState.Completed, 44100, 2, 44100);
        await recordingJournal.WriteAsync(new(recording with { Availability = RecordingAvailability.Available, Duration = capture.Duration }, 1, changed), new(capture, 1, changed));
        var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixture.Wave))).ToLowerInvariant();
        var source = new RecordingArtifact(Guid.NewGuid(), recording.Id, call.Id, RecordingArtifactFormat.Wave, "process.wav", RecordingAvailability.Pending);
        await fixture.Artifacts.WriteAsync(new(source, null, changed));
        await fixture.Artifacts.WriteAsync(new(new(source.Id, recording.Id, call.Id, source.Format, source.RelativePath, RecordingAvailability.Available, new FileInfo(fixture.Wave).Length, digest), 1, changed));
        var output = new RecordingArtifact(Guid.NewGuid(), recording.Id, call.Id, RecordingArtifactFormat.Mp3, "process.mp3", RecordingAvailability.Pending);
        var job = new RecordingConversionJob(Guid.NewGuid(), source.Id, output.Id, new(44100, 2, 128000), RecordingConversionState.Pending);
        await fixture.Journal.BeginAsync(new(output, null, changed), new(job, null, changed));
        var gate = new Gate(fixture, point, new(job.Id, source.Id, output.Id, recording.Id, local, digest));
        var conversions = new GatedOutbox(fixture.Conversions, gate);
        var converter = new GatedConverter(new WavToMp3Converter(new GatedBackend(gate)), gate);
        var factory = new PersistedMp3ConversionFactory(fixture.Jobs, fixture.Artifacts, fixture.Captures, fixture.Calls, new Identity(local), fixture.Paths,
            fixture.RecordingLeases, new LocalRecordingFileProbe(fixture.Paths), converter, new DurableRecordingConversionJournal(fixture.Journal, conversions), TimeProvider.System, conversions);
        await using var owner = await factory.StartAsync(job.Id);
        await owner.RunAsync();
    }
    internal static void ValidateServerTarget(DatabaseTarget target)
    {
        using var connection = new DatabaseFactory().CreateConnection(target);
        var name = connection.Database;
        if (!name.StartsWith("wcp_schema_", StringComparison.Ordinal) || !Guid.TryParseExact(name[11..], "N", out _))
            throw new AssertFailedException("Only a disposable provider-harness database is allowed.");
        var local = target.Type == DatabaseType.SqlServer
            ? string.Equals(connection.DataSource, "(localdb)\\WcpSchema" + name[11..], StringComparison.OrdinalIgnoreCase)
            : connection.DataSource == "127.0.0.1" || connection.DataSource.StartsWith("tcp://127.0.0.1:", StringComparison.Ordinal);
        if (!local) throw new AssertFailedException("Only local disposable provider instances are allowed.");
    }
    internal static async Task ConfigureServerAsync(string root, DatabaseTarget target, string engine)
    {
        ValidateRoot(root); ValidateServerTarget(target);
        using var connection = new DatabaseFactory().CreateConnection(target); var secrets = new WindowsUserSecretProtector();
        var provider = target.Type switch { DatabaseType.SqlServer => CommunicationDatabaseProvider.SqlServer,
            DatabaseType.MySql => CommunicationDatabaseProvider.MySql, DatabaseType.PostgreSql => CommunicationDatabaseProvider.PostgreSql,
            _ => throw new AssertFailedException() };
        await new ConfigurationStore(new(root), new ApplicationOptionsValidator(secrets)).SaveAsync(new() { Database = new()
            { Provider = provider, ConnectionString = secrets.Protect(connection.ConnectionString, CommunicationDatabaseSettings.ConnectionStringPurpose) } });
        await File.WriteAllTextAsync(Path.Combine(root, "engine.txt"), engine);
    }
    private sealed class Identity(Guid local) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult<Guid?>(local); public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new AssertFailedException("Recovery must not initialize an identity."); }

    private sealed class Gate(Mp3CrashFixture fixture, string point, Mp3CrashSeed seed)
    {
        internal bool Is(string value) => point == value;
        internal string Root => fixture.Paths.RootDirectory;
        internal void Stop(FileStream output)
        {
            if (output.CanWrite) output.Flush(true);
            var position = output.Position; output.Position = 0;
            var hash = Convert.ToHexString(SHA256.HashData(output)).ToLowerInvariant(); output.Position = position;
            var signal = new Mp3CrashSignal(point, Environment.ProcessId, typeof(Mp3ProcessCrashTests).Assembly.Location, typeof(WavToMp3Converter).Assembly.Location,
                seed, Path.GetRelativePath(fixture.Paths.RecordingsDirectory, output.Name).Replace('\\', '/'), output.Length, hash) { Engine = fixture.Engine };
            var temporary = Path.Combine(Root, "ready.json.tmp");
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(file, signal); file.Flush(true); }
            File.Move(temporary, Path.Combine(Root, "ready.json"), false);
            using var blocked = new ManualResetEventSlim(); blocked.Wait(); // Only the parent process may end this fixture.
        }
        internal void StopPublished()
        { using var output = new FileStream(fixture.Output, FileMode.Open, FileAccess.Read, FileShare.Read); Stop(output); }
    }
    private sealed class GatedBackend(Gate gate) : IMp3EncodingBackend
    {
        private readonly MediaFoundationMp3Backend _native = new();
        public ImmutableArray<Mp3EncodingFormat> GetSupportedFormats(CancellationToken token) => _native.GetSupportedFormats(token);
        public TimeSpan Encode(Stream wave, Stream output, Mp3EncodingFormat format, CancellationToken token)
        {
            File.AppendAllText(Path.Combine(gate.Root, "encoder-started"), "native\n");
            using var gated = new GatedWriteStream((FileStream)output, gate);
            var duration = _native.Encode(wave, gate.Is("encoding") ? gated : output, format, token);
            File.WriteAllText(Path.Combine(gate.Root, "encoder-returned"), "finalized"); return duration;
        }
    }
    private sealed class GatedWriteStream(FileStream output, Gate gate) : Stream
    {
        private readonly object _sync = new();
        public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => true;
        public override long Length { get { lock (_sync) return output.Length; } }
        public override long Position { get { lock (_sync) return output.Position; } set { lock (_sync) output.Position = value; } }
        public override void Flush() { lock (_sync) output.Flush(); }
        public override int Read(byte[] buffer, int offset, int count) { lock (_sync) return output.Read(buffer, offset, count); }
        public override long Seek(long offset, SeekOrigin origin) { lock (_sync) return output.Seek(offset, origin); }
        public override void SetLength(long value) { lock (_sync) output.SetLength(value); }
        public override void Write(byte[] buffer, int offset, int count)
        { lock (_sync) { output.Write(buffer, offset, count); if (count > 0) gate.Stop(output); } }
        protected override void Dispose(bool disposing) { /* The converter owns the original stream. */ }
    }
    private sealed class GatedConverter(IMp3Converter inner, Gate gate) : IMp3Converter
    {
        public Task<ImmutableArray<Mp3EncodingFormat>> GetSupportedFormatsAsync(CancellationToken token = default) => inner.GetSupportedFormatsAsync(token);
        public async Task<Mp3ConversionResult> ConvertAsync(Mp3ConversionRequest request, CancellationToken token = default)
        {
            var result = await inner.ConvertAsync(request with { Publication = new Observer(request.Publication!, gate) }, token);
            if (gate.Is("published")) gate.StopPublished();
            return result;
        }
        private sealed class Observer(IMp3PublicationObserver inner, Gate gate) : IMp3PublicationObserver
        {
            public void Prepare(string path, FileStream output, Mp3ConversionResult result, CancellationToken token)
            {
                if (gate.Is("before-preparation")) gate.Stop(output);
                inner.Prepare(path, output, result, token);
                if (gate.Is("prepared")) gate.Stop(output);
            }
        }
    }
    private sealed class GatedOutbox(IRecordingConversionOutbox inner, Gate gate) : IRecordingConversionOutbox
    {
        public IReadOnlyList<Guid> GetPending() => inner.GetPending(); public bool HasPending(Guid id) => inner.HasPending(id);
        public IAsyncDisposable AcquireEmpty(Guid id) => inner.AcquireEmpty(id);
        public void StagePrepared(RecordingArtifactWrite target, RecordingConversionWrite job, string relative, FileStream output, CancellationToken token = default) => inner.StagePrepared(target, job, relative, output, token);
        public Task<IRecordingConversionDelivery> OpenAsync(Guid id, CancellationToken token = default) => inner.OpenAsync(id, token);
        public Task<IRecordingConversionDelivery> ResumeAsync(StoredRecordingArtifact target, StoredRecordingConversion job, CancellationToken token = default) => inner.ResumeAsync(target, job, token);
        public async Task<IRecordingConversionDelivery> StageAsync(RecordingArtifactWrite target, RecordingConversionWrite job, CancellationToken token = default) => new Delivery(await inner.StageAsync(target, job, token), gate);
        private sealed class Delivery(IRecordingConversionDelivery inner, Gate gate) : IRecordingConversionDelivery
        {
            public RecordingArtifactWrite Target => inner.Target; public RecordingConversionWrite Conversion => inner.Conversion;
            public void Acknowledge() { if (gate.Is("db-confirmed")) gate.StopPublished(); inner.Acknowledge(); }
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
