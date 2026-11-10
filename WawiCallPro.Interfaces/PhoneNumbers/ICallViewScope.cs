using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Interfaces.PhoneNumbers;

/// <summary>Selection revision belongs to this call workspace, independently of SIP state revisions.</summary>
public sealed record CallViewContext(Guid CallId, long Revision, PartyKey Party, string CompanyName, string PartyName);
public interface ICallViewScopeFactory { ICallViewScope Create(CallSnapshot call); }
/// <summary>Owns cancellation, not consumer tasks; consumers must await their loads before disposing views.</summary>
public interface ICallViewScope : IAsyncDisposable
{
    Guid CallId { get; }
    CallSnapshot Call { get; }
    CallViewContext? Current { get; }
    bool IsEnded { get; }
    Task<bool> ObserveAsync(CallSnapshot call);
    Task<CallViewContext?> SelectAsync(CallerMatchDecision decision);
    ICallViewOperation BeginOperation(CancellationToken cancellationToken = default);
}
public interface ICallViewOperation : IDisposable
{
    CallViewContext Context { get; }
    CancellationToken CancellationToken { get; }
    ValueTask<bool> PublishAsync(IUiDispatcher dispatcher, Action publish);
}
