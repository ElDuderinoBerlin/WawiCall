using System.Collections.Immutable;

namespace WawiCallPro.Interfaces.Office;

public enum MailProviderKind { OutlookDesktop = 1, Microsoft365 = 2, Gmail = 3 }
public sealed record MailRecipient(string Address, string Name, string Kind);
public sealed record MailStore(string Id, string Name);
public sealed record MailEnvelope(MailContent Content, string? InternetMessageId, string SenderAddress,
    ImmutableArray<MailRecipient> Recipients, ImmutableArray<string> FolderIds, string Revision)
{
    public string? StableId { get; init; }
}
/// <summary>A checkpoint is committed only together with every change in this page. A null next page ends the round.</summary>
public sealed record MailChangePage(ImmutableArray<MailEnvelope> Messages, ImmutableArray<string> RemovedIds,
    string? NextPage, string? Checkpoint, bool Reset = false)
{
    public bool FullSnapshot { get; init; }
}

/// <summary>Read-only provider boundary. IDs belong to one configured account; reads never mark messages read.</summary>
public interface IMailConnector
{
    Guid AccountId { get; }
    string Provider { get; }
    Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default);
    Task<ImmutableArray<MailListItem>> GetMessagesAsync(string folderId, string search, CancellationToken cancellationToken = default);
    Task<MailEnvelope?> GetMessageAsync(string messageId, CancellationToken cancellationToken = default);
    Task<MailChangePage> GetChangesAsync(string folderId, string? checkpoint, string? nextPage, CancellationToken cancellationToken = default);
    Task<byte[]> GetAttachmentAsync(string messageId, string attachmentId, CancellationToken cancellationToken = default);
}

public enum MailFailure { Configuration, SignInRequired, AccessDenied, ProviderUnavailable, InvalidResponse, LimitExceeded, NotSupported, Conflict }
/// <summary>Safe diagnostics: provider errors, request URLs, account names, message content and tokens never escape.</summary>
public sealed class MailException(MailFailure failure) : Exception(failure switch
{
    MailFailure.Configuration => "Die Postfachkonfiguration ist unvollständig oder ungültig. Bitte die E-Mail-Einstellungen prüfen.",
    MailFailure.SignInRequired => "Bitte das Postfach in den E-Mail-Einstellungen anmelden oder erneut verbinden.",
    MailFailure.AccessDenied => "Der Zugriff auf das Postfach wurde abgelehnt. Leserechte und Anmeldung prüfen.",
    MailFailure.LimitExceeded => "Die Mailabfrage ist zu umfangreich. Zeitraum oder Ordnerauswahl einschränken.",
    MailFailure.NotSupported => "Diese Funktion wird von diesem Mailanbieter nicht unterstützt. Outlook Desktop benötigt das klassische Outlook.",
    MailFailure.Conflict => "Die Postfachkonfiguration oder der Synchronisierungsstand wurde zwischenzeitlich geändert. Bitte neu laden.",
    _ => "Das Postfach konnte nicht gelesen werden. Verbindung, Anbieter und Einstellungen prüfen."
})
{
    public MailFailure Failure { get; } = failure;
}
