using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Telephony;

public interface ICallStateSession
{
    CallSnapshot Current { get; }
    CallChangeResult Apply(CallStateChange change);
}

public interface ICallStateSessionFactory
{
    ICallStateSession Create(CallSnapshot initial);
}

/// <summary>Creates isolated state per call; the existing state machine remains the single transition authority.</summary>
public sealed class CallStateSessionFactory : ICallStateSessionFactory
{
    public ICallStateSession Create(CallSnapshot initial) => new Session(new CallStateMachine(initial));

    private sealed class Session(CallStateMachine machine) : ICallStateSession
    {
        public CallSnapshot Current => machine.Current;
        public CallChangeResult Apply(CallStateChange change) => machine.Apply(change);
    }
}
