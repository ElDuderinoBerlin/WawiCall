namespace WawiCallPro.Interfaces.Audio;

/// <summary>A verified sampling instant, never a packet/report arrival time. The caller must establish
/// the shared clock identity and actual synchronization; matching IDs alone do not prove either.</summary>
public sealed record RtpSamplingReference(Guid ReferenceClockId, uint SynchronizationSource, int RtpClockRate,
    uint Timestamp, ulong NtpTimestamp)
{
    public override string ToString() => nameof(RtpSamplingReference);
}

/// <summary>Two sampling references from the same source/clock, in chronological order.
/// NTP is unsigned 32.32 fixed-point, not a DateTime or a compact RTCP RTT timestamp.</summary>
public sealed record RtpClockCalibration(RtpSamplingReference Earlier, RtpSamplingReference Later)
{
    public override string ToString() => nameof(RtpClockCalibration);
}

/// <summary>Caller-verified synchronized references and selected start positions. RecordingStartNtp uses
/// the same reference clock. Raw SDK sender reports must not be assumed to be verified sampling references.</summary>
public sealed record RtpDuplexCalibration(Guid ReferenceClockId, ulong RecordingStartNtp,
    RtpClockCalibration Sent, RtpClockCalibration Received,
    SentRtpPcmPosition SentStart, RtpPcmPacketPosition ReceivedStart)
{
    public override string ToString() => nameof(RtpDuplexCalibration);
}

public interface IRtpRecordingAnchorResolver
{
    /// <summary>Checks supplied calibration consistency and calculates both recording-relative anchors.
    /// Performs no IO, clock acquisition or synchronization discovery. Does not correct ongoing clock drift.
    /// One to 60 seconds between references, 500 ppm plus one RTP unit discrepancy and two seconds
    /// between the selected start position and its latest reference are supported. Zero NTP means unavailable.</summary>
    DuplexPcmRecordingRequest Resolve(PcmRecordingRequest recording, RtpDuplexCalibration calibration);
}

public enum RtpRecordingCalibrationFailure
{
    InvalidReference = 1, ReferenceClockMismatch, SourceChanged, ClockRateChanged,
    ClockDiscontinuity, ReferenceTooDistant, OffsetOutOfRange
}

/// <summary>Fixed diagnostics without timestamps, source identities or filenames.</summary>
public sealed class RtpRecordingCalibrationException(RtpRecordingCalibrationFailure failure) : Exception(failure switch
{
    RtpRecordingCalibrationFailure.ReferenceClockMismatch => "Die Aufnahme benötigt dieselbe verifizierte Referenzuhr für beide Gesprächsseiten.",
    RtpRecordingCalibrationFailure.SourceChanged => "Die Audioquelle stimmt nicht mit der Aufnahmezeitreferenz überein.",
    RtpRecordingCalibrationFailure.ClockRateChanged => "Die RTP-Taktrate stimmt nicht mit der Aufnahmezeitreferenz überein.",
    RtpRecordingCalibrationFailure.ClockDiscontinuity => "Die Aufnahmezeitreferenzen sind zeitlich widersprüchlich oder überschreiten die unterstützte Taktabweichung.",
    RtpRecordingCalibrationFailure.ReferenceTooDistant => "Die Audioposition ist zu weit von der Aufnahmezeitreferenz entfernt.",
    RtpRecordingCalibrationFailure.OffsetOutOfRange => "Die berechnete Aufnahmeposition liegt außerhalb des unterstützten Zeitfensters.",
    _ => "Vollständige verifizierte Aufnahmezeitreferenzen sind erforderlich."
})
{
    public RtpRecordingCalibrationFailure Failure { get; } = failure;
}
