using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Audio;

/// <summary>Bounded fixed-point mapping from caller-verified sampling clocks to the existing recording contracts.</summary>
public sealed class RtpRecordingAnchorResolver : IRtpRecordingAnchorResolver
{
    private const long NtpUnitsPerSecond = 1L << 32;
    private const int MaximumClockRate = 192000;

    public DuplexPcmRecordingRequest Resolve(PcmRecordingRequest recording, RtpDuplexCalibration calibration)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(calibration);
        if (calibration.ReferenceClockId == Guid.Empty || calibration.RecordingStartNtp == 0
            || calibration.Sent is null || calibration.Received is null)
            throw Failure(RtpRecordingCalibrationFailure.InvalidReference);
        var sent = calibration.SentStart;
        var received = calibration.ReceivedStart;
        if (sent.DurationRtpUnits == 0 || sent.DurationRtpUnits > sent.RtpClockRate * 2L)
            throw Failure(RtpRecordingCalibrationFailure.InvalidReference);
        var sentOffset = ResolveOffset(calibration, calibration.Sent, sent.SynchronizationSource, sent.RtpClockRate, sent.Timestamp);
        var receivedOffset = ResolveOffset(calibration, calibration.Received, received.SynchronizationSource, received.RtpClockRate, received.Timestamp);
        return new(recording, new(sent.SynchronizationSource, sent.Timestamp, sent.RtpClockRate, sentOffset),
            new(received.SynchronizationSource, received.SequenceNumber, received.Timestamp, received.RtpClockRate, receivedOffset));
    }

    private static TimeSpan ResolveOffset(RtpDuplexCalibration request, RtpClockCalibration calibration, uint ssrc, int rate, uint timestamp)
    {
        var earlier = calibration.Earlier;
        var later = calibration.Later;
        if (earlier is null || later is null || earlier.NtpTimestamp == 0 || later.NtpTimestamp == 0
            || earlier.RtpClockRate is < 1 or > MaximumClockRate || later.RtpClockRate is < 1 or > MaximumClockRate
            || rate is < 1 or > MaximumClockRate)
            throw Failure(RtpRecordingCalibrationFailure.InvalidReference);
        if (earlier.ReferenceClockId != request.ReferenceClockId || later.ReferenceClockId != request.ReferenceClockId)
            throw Failure(RtpRecordingCalibrationFailure.ReferenceClockMismatch);
        if (earlier.SynchronizationSource != ssrc || later.SynchronizationSource != ssrc)
            throw Failure(RtpRecordingCalibrationFailure.SourceChanged);
        if (earlier.RtpClockRate != rate || later.RtpClockRate != rate)
            throw Failure(RtpRecordingCalibrationFailure.ClockRateChanged);

        // Signed modular differences support both RTP rollover and the NTP era rollover. The bounded
        // observation window excludes ambiguous half-cycle differences; no absolute calendar conversion is needed.
        var ntpElapsed = unchecked((long)(later.NtpTimestamp - earlier.NtpTimestamp));
        var rtpElapsed = unchecked((int)(later.Timestamp - earlier.Timestamp));
        if (ntpElapsed < NtpUnitsPerSecond || ntpElapsed > 60 * NtpUnitsPerSecond || rtpElapsed <= 0)
            throw Failure(RtpRecordingCalibrationFailure.ClockDiscontinuity);
        var expected = (Int128)ntpElapsed * rate;
        var discrepancy = Int128.Abs((Int128)rtpElapsed * NtpUnitsPerSecond - expected);
        // A one-unit quantization allowance is separate from the 500-ppm slope check. This is a
        // consistency check, not proof of synchronization, precision or absence of a constant clock offset.
        if (discrepancy > NtpUnitsPerSecond + expected * 500 / 1000000)
            throw Failure(RtpRecordingCalibrationFailure.ClockDiscontinuity);

        var distance = (long)unchecked((int)(timestamp - later.Timestamp));
        if (Math.Abs(distance) > rate * 2L)
            throw Failure(RtpRecordingCalibrationFailure.ReferenceTooDistant);
        var ntpPosition = unchecked(later.NtpTimestamp + (ulong)Round((Int128)distance * NtpUnitsPerSecond, rate));
        var ntpOffset = unchecked((long)(ntpPosition - request.RecordingStartNtp));
        if (ntpOffset < -2 * NtpUnitsPerSecond || ntpOffset >= 8 * 3600 * NtpUnitsPerSecond)
            throw Failure(RtpRecordingCalibrationFailure.OffsetOutOfRange);
        var ticks = Round((Int128)ntpOffset * TimeSpan.TicksPerSecond, NtpUnitsPerSecond);
        if (ticks < -2 * TimeSpan.TicksPerSecond || ticks >= TimeSpan.TicksPerHour * 8)
            throw Failure(RtpRecordingCalibrationFailure.OffsetOutOfRange);
        return TimeSpan.FromTicks(ticks);
    }

    private static long Round(Int128 numerator, long denominator) => checked((long)(numerator >= 0
        ? (numerator + denominator / 2) / denominator
        : -((-numerator + denominator / 2) / denominator)));
    private static RtpRecordingCalibrationException Failure(RtpRecordingCalibrationFailure failure) => new(failure);
}
