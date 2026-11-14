using System.Collections.Immutable;
using WawiCallPro.Interfaces.Office;

namespace WawiCallPro.App.Hosting;

/// <summary>Explicit empty state until a configured provider is installed in the composition root.</summary>
internal sealed class UnconfiguredMailViewSource : IMailViewSource
{
    public Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new MailFolderCatalog([], "Kein Postfach verbunden. Die Einrichtung der Mail-Anbieter ist noch nicht verfügbar."));
    public Task<ImmutableArray<MailListItem>> GetMessagesAsync(MailFolderKey folder, string search, CancellationToken cancellationToken = default) =>
        Task.FromResult(ImmutableArray<MailListItem>.Empty);
    public Task<MailContent?> GetMessageAsync(MailMessageKey message, CancellationToken cancellationToken = default) => Task.FromResult<MailContent?>(null);
}
