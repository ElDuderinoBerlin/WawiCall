using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Console.Tests;

public sealed partial class SentRtpPcmTests
{
    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task SuccessfulCaptureConfirmsTheWaveArtifactBeforeStopCompletes(SDPWellKnownMediaFormatsEnum codec)
    {
        await using var fixture = new Fixture(codec); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        var pending = await Pending(files); var backend = Backend(new SwitchTarget(files.Target));
        await using var media = files.Media(fixture, new DurableRecordingJournal(backend, pending, Publisher(files)));
        var recording = await media.StartRecordingAsync(request); fixture.Capture(Samples(fixture.Format.ClockRate / 50, 0)); await fixture.Packets(1);
        await recording.StopAsync(TimeSpan.FromMilliseconds(40));
        var bytes = File.ReadAllBytes(files.Output); var artifact = (await files.Artifacts()).Single();
        Assert.AreEqual(request.Recording.RecordingId, artifact.Artifact.Id); Assert.AreEqual(2L, artifact.Version);
        Assert.AreEqual(RecordingArtifactFormat.Wave, artifact.Artifact.Format); Assert.AreEqual(RecordingAvailability.Available, artifact.Artifact.Availability);
        Assert.AreEqual(bytes.LongLength, artifact.Artifact.ByteLength);
        Assert.AreEqual(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(), artifact.Artifact.Sha256);
        Assert.AreEqual(0, pending.GetPending().Count); Assert.AreEqual(1, fixture.Codec.Encodes);
    }
}
