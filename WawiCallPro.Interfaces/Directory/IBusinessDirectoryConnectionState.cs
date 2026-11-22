namespace WawiCallPro.Interfaces.Directory;

/// <summary>Optional local setup status. Reading it never authenticates or contacts the business source.</summary>
public interface IBusinessDirectoryConnectionState
{
    bool IsConnectionConfigured { get; }
}
