using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Audio;

/// <summary>Creates explicit receive subscriptions; file/session lifetime stays with the caller.</summary>
public sealed class ReceivedPcmRecordingBindingFactory(IRtpPcmRecordingFeedFactory feeds) : IReceivedPcmRecordingBindingFactory
{
    public async Task<IReceivedPcmRecordingBinding> AttachAsync(IConversationPcmFailureSource source,
        IPcmRecordingSession session, RtpPcmTimelineAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(session);
        if (session is not IPcmRecordingInputGuard guard || session.State != PcmRecordingState.Capturing)
            throw new ArgumentException("Die aufnehmende Sitzung benötigt eine wirksame Eingabesperre für Medienfehler.", nameof(session));
        var feed = feeds.Create(session, ConversationAudioSide.Received, anchor);
        try { return new ReceivedPcmRecordingBinding(source, feed, guard); }
        catch
        {
            try { guard.RejectInput(PcmRecordingFailure.TimelineConflict); }
            finally { await feed.DisposeAsync().ConfigureAwait(false); }
            throw;
        }
    }
}
