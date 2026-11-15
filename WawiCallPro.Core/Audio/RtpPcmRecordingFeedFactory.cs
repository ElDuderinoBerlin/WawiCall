using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Audio;

/// <summary>Creates isolated per-side timelines without acquiring files, devices or transports.</summary>
public sealed class RtpPcmRecordingFeedFactory : IRtpPcmRecordingFeedFactory
{
    public IRtpPcmRecordingFeed Create(IPcmRecordingSession session, ConversationAudioSide side, RtpPcmTimelineAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(anchor);
        if (!Enum.IsDefined(side) || anchor.RtpClockRate is < 1 or > 192000 ||
            anchor.RecordingOffset < TimeSpan.FromSeconds(-2) || anchor.RecordingOffset >= TimeSpan.FromHours(8))
            throw new ArgumentException("Ein gültiger expliziter RTP-Aufnahmeanker ist erforderlich.");
        if (session is not IPcmRecordingInputGuard guard || session.State != PcmRecordingState.Capturing)
            throw new ArgumentException("Die aufnehmende Sitzung benötigt eine wirksame Eingabesperre für Medienfehler.");
        return new RtpPcmRecordingFeed(session, guard, side, anchor);
    }
}
