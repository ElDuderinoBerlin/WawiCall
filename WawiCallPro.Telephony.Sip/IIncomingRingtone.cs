namespace WawiCallPro.Telephony.Sip;

/// <summary>Plays a local incoming-call signal until cancelled. Completion includes output disposal.</summary>
public interface IIncomingRingtone
{
    Task PlayAsync(string? deviceId, int volumePercent, CancellationToken cancellationToken);
}
