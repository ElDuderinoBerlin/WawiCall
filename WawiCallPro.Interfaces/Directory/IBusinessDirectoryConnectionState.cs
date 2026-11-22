namespace WawiCallPro.Interfaces.Directory;

/// <summary>Optional local setup status. Reading it never authenticates or contacts the business source.</summary>
public interface IBusinessDirectoryConnectionState
{
    long ConnectionRevision => 0;
    bool IsConnectionConfigured { get; }
}
