namespace WawiCallPro.Utilities.Operations;

/// <summary>Application-defined operational messages without native paths, arbitrary messages or credentials.</summary>
public sealed class OperationException : Exception
{
    private OperationException(string message) : base(message) { }
    internal static OperationException InvalidPath() => new("Der Betriebspfad ist ungültig oder liegt außerhalb des vorgesehenen Verzeichnisses.");
    internal static OperationException LogUnavailable() => new("Das Betriebsprotokoll konnte nicht geschrieben werden.");
}
