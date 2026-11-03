namespace WawiCallPro.Database.Contracts;

public enum RepositoryConflictKind
{
    AlreadyExists = 1,
    VersionMismatch = 2,
    MissingRecord = 3,
    IdentityCollision = 4,
    OwnershipMismatch = 5,
    AppendConflict = 6
}

/// <summary>Expected persistence conflicts expose a fixed code/message, never rejected record values or provider exception contents.</summary>
public sealed class RepositoryConflictException : Exception
{
    public RepositoryConflictException(RepositoryConflictKind kind) : base(kind switch
    {
        RepositoryConflictKind.AlreadyExists => "Der Datensatz besteht bereits. Vor der Änderung erneut laden.",
        RepositoryConflictKind.VersionMismatch => "Der Datensatz wurde zwischenzeitlich geändert. Erneut laden.",
        RepositoryConflictKind.MissingRecord => "Der erwartete Datensatz ist nicht mehr vorhanden.",
        RepositoryConflictKind.IdentityCollision => "Die gespeicherte Quellidentität stimmt nicht mit dem Indexschlüssel überein.",
        RepositoryConflictKind.OwnershipMismatch => "Der Datensatz gehört zu einem anderen Gespräch oder Mandanten.",
        RepositoryConflictKind.AppendConflict => "Ein bereits gespeichertes Ereignis darf nicht verändert werden.",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    }) => Kind = kind;

    public RepositoryConflictKind Kind { get; }
}
