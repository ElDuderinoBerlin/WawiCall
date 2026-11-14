using System.Collections.Immutable;

namespace WawiCallPro.Interfaces.Office;

/// <summary>Source IDs are opaque and scoped to both provider and account.</summary>
public sealed record MailFolderKey(string Provider, string Account, string Id);
public sealed record MailMessageKey(string Provider, string Account, string Id);
public sealed record MailFolder(MailFolderKey Key, MailFolderKey? Parent, string Name, int? UnreadCount = null);
public sealed record MailFolderCatalog(ImmutableArray<MailFolder> Folders, string Status);
public sealed record MailListItem(MailMessageKey Key, MailFolderKey Folder, string Sender, string Subject,
    DateTimeOffset Date, string Preview, bool IsRead, int AttachmentCount);
public sealed record MailAttachmentInfo(string Name, long? Size);
/// <summary>Detached, read-only content. HTML resources and hyperlinks require explicit UI handling.</summary>
public sealed record MailContent(MailMessageKey Key, string Subject, string From, string To, string Cc,
    DateTimeOffset Date, string Text, string? Html, ImmutableArray<MailAttachmentInfo> Attachments);

/// <summary>
/// Supplies a mail browser independently of the provider SDK. Search applies to the complete source folder;
/// each result is a complete snapshot for that query, not a silently truncated page. No read changes mailbox state.
/// </summary>
public interface IMailViewSource
{
    Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default);
    Task<ImmutableArray<MailListItem>> GetMessagesAsync(MailFolderKey folder, string search, CancellationToken cancellationToken = default);
    Task<MailContent?> GetMessageAsync(MailMessageKey message, CancellationToken cancellationToken = default);
}
