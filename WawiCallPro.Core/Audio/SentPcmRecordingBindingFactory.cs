using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Audio;

/// <summary>Creates explicit source-timeline subscriptions without files, devices or timers.</summary>
public sealed class SentPcmRecordingBindingFactory : ISentPcmRecordingBindingFactory
{
    public ISentPcmRecordingBinding Attach(IConversationPcmFailureSource source, IPcmRecordingSession session, SentPcmTimelineAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(anchor);
        if (anchor.RtpClockRate is < 1 or > 192000 || anchor.RecordingOffset < TimeSpan.FromSeconds(-2) || anchor.RecordingOffset >= TimeSpan.FromHours(8))
            throw new ArgumentException("Ein gültiger expliziter Sende-Aufnahmeanker ist erforderlich.", nameof(anchor));
        if (session is not IPcmRecordingInputGuard guard || session.State != PcmRecordingState.Capturing)
            throw new ArgumentException("Die aufnehmende Sitzung benötigt eine wirksame Eingabesperre für Medienfehler.", nameof(session));
        try { return new SentPcmRecordingBinding(source, session, guard, anchor); }
        catch
        {
            try { guard.RejectInput(PcmRecordingFailure.TimelineConflict); }
            catch (Exception) { /* Keep the original subscription error visible even for a faulty foreign guard. */ }
            throw;
        }
    }
}
