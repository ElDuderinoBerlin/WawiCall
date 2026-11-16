using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class RtpRecordingAnchorResolverTests
{
    private const ulong Second = 1UL << 32;
    private const ulong Epoch = 0xE000000080000000;
    private static readonly Guid Clock = Guid.NewGuid();
    private static PcmRecordingRequest Recording() => new(Guid.NewGuid(), Guid.NewGuid(), "unused.wav");
    private static RtpClockCalibration Calibration(uint ssrc, uint timestamp, int rate, ulong ntp) => new(
        new(Clock, ssrc, rate, unchecked(timestamp - (uint)rate), unchecked(ntp - Second)), new(Clock, ssrc, rate, timestamp, ntp));
    private static RtpDuplexCalibration Request(int sentRate = 8000, int receivedRate = 8000, ulong epoch = Epoch) => new(Clock, epoch,
        Calibration(17, 100000, sentRate, epoch), Calibration(91, 200000, receivedRate, epoch + Second / 8),
        new(17, 100000, sentRate, (uint)(sentRate / 50)), new(91, 65535, 200000, receivedRate));
    private static DuplexPcmRecordingRequest Resolve(RtpDuplexCalibration request) => new RtpRecordingAnchorResolver().Resolve(Recording(), request);

    [TestMethod]
    [DataRow(8000, 8000)]
    [DataRow(8000, 48000)]
    [DataRow(44100, 16000)]
    [DataRow(192000, 44100)]
    public void IndependentRtpOriginsAndRatesProduceTheSameCommonEpoch(int sentRate, int receivedRate)
    {
        var recording = Recording(); var input = Request(sentRate, receivedRate); var result = new RtpRecordingAnchorResolver().Resolve(recording, input);
        Assert.AreSame(recording, result.Recording); Assert.AreEqual(TimeSpan.Zero, result.Sent.RecordingOffset);
        Assert.AreEqual(TimeSpan.FromMilliseconds(125), result.Received.RecordingOffset);
        Assert.AreEqual(input.SentStart.Timestamp, result.Sent.Timestamp); Assert.AreEqual(sentRate, result.Sent.RtpClockRate);
        Assert.AreEqual(input.ReceivedStart.Timestamp, result.Received.Timestamp); Assert.AreEqual(receivedRate, result.Received.RtpClockRate);
        Assert.AreEqual((ushort)65535, result.Received.SequenceNumber);
    }

    [TestMethod]
    public void RtpAndNtpEraRolloversDoNotChangeRelativeOffsets()
    {
        const ulong epoch = ulong.MaxValue - Second / 4; var request = Request(epoch: epoch);
        var sent = Calibration(17, uint.MaxValue - 159, 8000, epoch);
        var received = Calibration(91, 79, 8000, unchecked(epoch + Second / 2));
        var result = Resolve(request with { Sent = sent, Received = received,
            SentStart = new(17, 0, 8000, 160), ReceivedStart = new(91, 0, 159, 8000) });
        Assert.AreEqual(TimeSpan.FromMilliseconds(20), result.Sent.RecordingOffset);
        Assert.AreEqual(TimeSpan.FromMilliseconds(510), result.Received.RecordingOffset);
    }

    [TestMethod]
    public void NegativeProjectionAndMidPacketRecordingUseSymmetricTickRounding()
    {
        var input = Request(44100, 44100);
        var result = Resolve(input with { SentStart = input.SentStart with { Timestamp = input.SentStart.Timestamp - 1 },
            ReceivedStart = input.ReceivedStart with { Timestamp = input.ReceivedStart.Timestamp + 1 } });
        Assert.AreEqual(TimeSpan.FromTicks(-227), result.Sent.RecordingOffset);
        Assert.AreEqual(TimeSpan.FromMilliseconds(125) + TimeSpan.FromTicks(227), result.Received.RecordingOffset);
    }

    [TestMethod]
    [DataRow(-16000)]
    [DataRow(16000)]
    public void ExactlyTwoSecondsFromTheLatestReferenceAreSupported(int delta)
    {
        var input = Request(); var result = Resolve(input with { SentStart = input.SentStart with { Timestamp = unchecked((uint)(100000 + delta)) } });
        Assert.AreEqual(TimeSpan.FromSeconds(delta / 8000D), result.Sent.RecordingOffset);
    }

    [TestMethod]
    [DataRow(-16001)]
    [DataRow(16001)]
    [DataRow(int.MinValue)]
    public void DistantAndAmbiguousRtpPositionsAreRejected(int delta)
    {
        var input = Request(); AssertFailure(input with { SentStart = input.SentStart with { Timestamp = unchecked((uint)(100000L + delta)) } },
            RtpRecordingCalibrationFailure.ReferenceTooDistant);
    }

    [TestMethod]
    [DataRow("domain")]
    [DataRow("source")]
    [DataRow("rate")]
    public void AChangeBetweenReferencesOrSelectedPositionCannotBeSilentlyAccepted(string change)
    {
        var input = Request(); var earlier = input.Received.Earlier;
        var changed = change switch
        {
            "domain" => earlier with { ReferenceClockId = Guid.NewGuid() },
            "source" => earlier with { SynchronizationSource = 123 },
            _ => earlier with { RtpClockRate = 16000 }
        };
        var expected = change switch { "domain" => RtpRecordingCalibrationFailure.ReferenceClockMismatch,
            "source" => RtpRecordingCalibrationFailure.SourceChanged, _ => RtpRecordingCalibrationFailure.ClockRateChanged };
        AssertFailure(input with { Received = input.Received with { Earlier = changed } }, expected);
        if (change == "source") AssertFailure(input with { ReceivedStart = input.ReceivedStart with { SynchronizationSource = 123 } }, expected);
        if (change == "rate") AssertFailure(input with { ReceivedStart = input.ReceivedStart with { RtpClockRate = 16000 } }, expected);
    }

    [TestMethod]
    [DataRow("epoch")]
    [DataRow("clock")]
    [DataRow("ntp")]
    [DataRow("rate-low")]
    [DataRow("rate-high")]
    [DataRow("duration-zero")]
    [DataRow("duration-long")]
    [DataRow("null-calibration")]
    [DataRow("null-reference")]
    public void MissingOrUnsupportedReferenceDataIsRejected(string invalid)
    {
        var input = Request(); var earlier = input.Sent.Earlier;
        input = invalid switch
        {
            "epoch" => input with { RecordingStartNtp = 0 },
            "clock" => input with { ReferenceClockId = Guid.Empty },
            "ntp" => input with { Sent = input.Sent with { Earlier = earlier with { NtpTimestamp = 0 } } },
            "rate-low" => input with { Sent = input.Sent with { Earlier = earlier with { RtpClockRate = 0 } } },
            "rate-high" => input with { Sent = input.Sent with { Earlier = earlier with { RtpClockRate = 192001 } } },
            "duration-zero" => input with { SentStart = input.SentStart with { DurationRtpUnits = 0 } },
            "duration-long" => input with { SentStart = input.SentStart with { DurationRtpUnits = 16001 } },
            "null-calibration" => input with { Received = null! },
            _ => input with { Sent = input.Sent with { Earlier = null! } }
        };
        AssertFailure(input, RtpRecordingCalibrationFailure.InvalidReference);
    }

    [TestMethod]
    [DataRow("ntp-backward")]
    [DataRow("ntp-equal")]
    [DataRow("ntp-short")]
    [DataRow("ntp-long")]
    [DataRow("rtp-backward")]
    [DataRow("rtp-equal")]
    [DataRow("rtp-half-cycle")]
    [DataRow("rtp-wrong-slope")]
    public void ClockDiscontinuitiesCannotGenerateRecordingAnchors(string invalid)
    {
        var input = Request(); var later = input.Sent.Later;
        var changed = invalid switch
        {
            "ntp-backward" => later with { NtpTimestamp = input.Sent.Earlier.NtpTimestamp - Second },
            "ntp-equal" => later with { NtpTimestamp = input.Sent.Earlier.NtpTimestamp },
            "ntp-short" => later with { NtpTimestamp = input.Sent.Earlier.NtpTimestamp + Second - 1 },
            "ntp-long" => later with { NtpTimestamp = input.Sent.Earlier.NtpTimestamp + Second * 61 },
            "rtp-backward" => later with { Timestamp = input.Sent.Earlier.Timestamp - 1 },
            "rtp-equal" => later with { Timestamp = input.Sent.Earlier.Timestamp },
            "rtp-half-cycle" => later with { Timestamp = unchecked(input.Sent.Earlier.Timestamp + 0x80000000) },
            _ => later with { Timestamp = later.Timestamp + 6 }
        };
        AssertFailure(input with { Sent = input.Sent with { Later = changed } }, RtpRecordingCalibrationFailure.ClockDiscontinuity);
    }

    [TestMethod]
    public void QuantizationAndTheDocumentedSlopeBoundaryAreAcceptedWithoutClaimingDriftCorrection()
    {
        var input = Request(); var sent = input.Sent;
        // One second at 8 kHz: 4 units at 500 ppm plus the separate one-unit quantization allowance.
        var result = Resolve(input with { Sent = sent with { Earlier = sent.Earlier with { Timestamp = sent.Earlier.Timestamp - 5 } } });
        Assert.AreEqual(TimeSpan.Zero, result.Sent.RecordingOffset);
        AssertFailure(input with { Sent = sent with { Earlier = sent.Earlier with { Timestamp = sent.Earlier.Timestamp - 6 } } },
            RtpRecordingCalibrationFailure.ClockDiscontinuity);
    }

    [TestMethod]
    public void MaximumReferenceIntervalIsBoundedAndHandlesHighRateArithmetic()
    {
        var input = Request(192000, 192000); var sent = input.Sent;
        var result = Resolve(input with { Sent = sent with { Earlier = sent.Earlier with {
            Timestamp = unchecked(sent.Later.Timestamp - 192000U * 60), NtpTimestamp = sent.Later.NtpTimestamp - Second * 60 } } });
        Assert.AreEqual(TimeSpan.Zero, result.Sent.RecordingOffset);
    }

    [TestMethod]
    public void OffsetBoundsAreCheckedBeforeAndAfterTickRounding()
    {
        var input = Request();
        AssertFailure(input with { RecordingStartNtp = input.RecordingStartNtp + Second * 3 }, RtpRecordingCalibrationFailure.OffsetOutOfRange);
        AssertFailure(input with { RecordingStartNtp = input.RecordingStartNtp - Second * 28800 }, RtpRecordingCalibrationFailure.OffsetOutOfRange);
        AssertFailure(input with { RecordingStartNtp = input.RecordingStartNtp - Second * 28800 + 1 }, RtpRecordingCalibrationFailure.OffsetOutOfRange);
        var lower = Resolve(input with { RecordingStartNtp = input.RecordingStartNtp + Second * 2 });
        Assert.AreEqual(TimeSpan.FromSeconds(-2), lower.Sent.RecordingOffset);
    }

    [TestMethod]
    public void NullArgumentsAndDiagnosticsDoNotExposeClockOrFileDetails()
    {
        var resolver = new RtpRecordingAnchorResolver(); Assert.ThrowsExactly<ArgumentNullException>(() => resolver.Resolve(null!, Request()));
        Assert.ThrowsExactly<ArgumentNullException>(() => resolver.Resolve(Recording(), null!)); var input = Request();
        Assert.AreEqual(nameof(RtpDuplexCalibration), input.ToString()); Assert.AreEqual(nameof(RtpClockCalibration), input.Sent.ToString());
        Assert.AreEqual(nameof(RtpSamplingReference), input.Sent.Later.ToString());
        var error = Assert.ThrowsExactly<RtpRecordingCalibrationException>(() => Resolve(input with { ReferenceClockId = Guid.NewGuid() }));
        Assert.IsNull(error.InnerException); Assert.IsFalse(error.ToString().Contains(Clock.ToString(), StringComparison.Ordinal));
        Assert.IsFalse(error.ToString().Contains("unused.wav", StringComparison.Ordinal));
    }

    private static void AssertFailure(RtpDuplexCalibration input, RtpRecordingCalibrationFailure expected)
    { var error = Assert.ThrowsExactly<RtpRecordingCalibrationException>(() => Resolve(input)); Assert.AreEqual(expected, error.Failure); }
}
