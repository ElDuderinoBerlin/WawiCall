using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Interfaces.History;

/// <summary>Stores call-specific evidence using the current call-state version. Existing historical evidence remains immutable.</summary>
public interface ICallAssociationService
{
    Task<CallHistory> SaveAsync(Guid callId, CallerMatchDecision decision, CancellationToken cancellationToken = default);
}
