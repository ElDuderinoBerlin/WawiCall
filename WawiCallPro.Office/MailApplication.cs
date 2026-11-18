using WawiCallPro.Interfaces.Office;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Office;

/// <summary>Application facade keeps view dependencies and credential handling outside WinForms.</summary>
public sealed class MailApplication(MailAccounts accounts, IMailSynchronization synchronization, MailCacheService browser,
    IMailConnectorFactory connectors)
{
    public MailAccounts Accounts { get; } = accounts;
    public IMailSynchronization Synchronization { get; } = synchronization;
    public IMailCacheMaintenance Cache => browser;
    public IMailViewSource ForParty(Party party) => browser.ForParty(party);
    public async Task<byte[]> DownloadAsync(MailMessageKey key, string attachmentId, CancellationToken cancellationToken)
    {
        if (await browser.GetMessageAsync(key, cancellationToken) is not { } message || !message.Attachments.Any(item => item.Id == attachmentId)) throw new MailException(MailFailure.AccessDenied);
        if (!Guid.TryParseExact(key.Account, "N", out var id)) throw new MailException(MailFailure.AccessDenied);
        var account = await Accounts.GetAsync(id, cancellationToken); var connector = connectors.Create(account);
        if (!account.Enabled || connector.Provider != key.Provider) throw new MailException(MailFailure.AccessDenied);
        if (await browser.IsEnabledAsync(cancellationToken)) return await browser.GetAttachmentAsync(key, attachmentId, cancellationToken);
        var bytes = await connector.GetAttachmentAsync(key.Id, attachmentId, cancellationToken);
        if (bytes.Length > MailLimits.AttachmentBytes) throw new MailException(MailFailure.LimitExceeded);
        return bytes;
    }
}
