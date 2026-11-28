using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Telephony.Sip;

/// <summary>Exclusive, ordered local delivery journal. Only acknowledge after a successful database commit.</summary>
public interface ISipCallOutbox : IDisposable
{
    Task<IReadOnlyList<CallSnapshot>> OpenAsync(CommunicationDatabaseSettings database, CancellationToken cancellationToken = default);
    Task AppendAsync(CallSnapshot snapshot, CancellationToken cancellationToken = default);
    void Acknowledge(CallSnapshot snapshot);
    /// <summary>Retires the oldest entry after a permanent rejection. It stays protected on disk but is not delivered again.</summary>
    void Quarantine(CallSnapshot snapshot);
    void Close();
}
