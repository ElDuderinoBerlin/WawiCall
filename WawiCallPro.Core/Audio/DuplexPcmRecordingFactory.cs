using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Audio;

/// <summary>Owns startup rollback and one duplex recording lifetime. Source and time calibration stay with the caller.</summary>
public sealed class DuplexPcmRecordingFactory(IPcmRecordingSessionFactory sessions,
    ISentPcmRecordingBindingFactory sentBindings, IReceivedPcmRecordingBindingFactory receivedBindings) : IDuplexPcmRecordingFactory
{
    public async Task<IDuplexPcmRecording> StartAsync(IConversationPcmFailureSource source, DuplexPcmRecordingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Recording); ArgumentNullException.ThrowIfNull(request.Sent); ArgumentNullException.ThrowIfNull(request.Received);
        Validate(request.Sent.RtpClockRate, request.Sent.RecordingOffset);
        Validate(request.Received.RtpClockRate, request.Received.RecordingOffset);
        cancellationToken.ThrowIfCancellationRequested();
        var session = await sessions.StartAsync(request.Recording, cancellationToken).ConfigureAwait(false);
        if (session is not IPcmRecordingInputGuard guard)
        { await session.DisposeAsync().ConfigureAwait(false); throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict); }
        var forwarder = new DuplexPcmRecordingSource(guard);
        ISentPcmRecordingBinding? sent = null; IReceivedPcmRecordingBinding? received = null; IConversationPcmSubscription? subscription = null;
        try
        {
            sent = sentBindings.Attach(forwarder.Sent, session, request.Sent);
            received = await receivedBindings.AttachAsync(forwarder.Received, session, request.Received).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            // Both private observers are ready before the first live callback can enter.
            subscription = source.Subscribe(forwarder);
            cancellationToken.ThrowIfCancellationRequested();
            if (session.State != PcmRecordingState.Capturing || subscription.Completion.IsCompleted)
                throw new PcmRecordingException(session.Failure ?? PcmRecordingFailure.TimelineConflict);
            cancellationToken.ThrowIfCancellationRequested();
            return new DuplexPcmRecording(session, guard, subscription, forwarder, sent, received, request.Recording);
        }
        catch
        {
            try
            {
                if (subscription is not null)
                {
                    try { await subscription.DisposeAsync().ConfigureAwait(false); }
                    finally { await subscription.Completion.ConfigureAwait(false); }
                }
            }
            finally
            {
                forwarder.Complete(PcmObservationFailure.ObserverFailed);
                try { if (sent is not null) await sent.DisposeAsync().ConfigureAwait(false); }
                finally
                {
                    try { if (received is not null) await received.DisposeAsync().ConfigureAwait(false); }
                    finally { await session.DisposeAsync().ConfigureAwait(false); }
                }
            }
            throw;
        }
    }

    private static void Validate(int rate, TimeSpan offset)
    {
        if (rate is < 1 or > 192000 || offset < TimeSpan.FromSeconds(-2) || offset >= TimeSpan.FromHours(8))
            throw new ArgumentException("Zwei gültige, aufeinander bezogene Aufnahmeanker sind erforderlich.");
    }
}
