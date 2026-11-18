using WawiCallPro.Interfaces.Office;
using WawiCallPro.Interfaces.Parties;
using System.Security.Cryptography;

namespace WawiCallPro.Office;

/// <summary>Application facade keeps view dependencies and credential handling outside WinForms.</summary>
public sealed class MailApplication(MailAccounts accounts, IMailSynchronization synchronization, MailCacheService browser,
    IMailConnectorFactory connectors)
{
    private readonly PartyMailScopeResolver? _partyScopes;
    public MailApplication(MailAccounts accounts, IMailSynchronization synchronization, MailCacheService browser,
        IMailConnectorFactory connectors, PartyMailScopeResolver partyScopes) : this(accounts, synchronization, browser, connectors)
        => _partyScopes = partyScopes;
    public MailAccounts Accounts { get; } = accounts;
    public IMailSynchronization Synchronization { get; } = synchronization;
    public IMailCacheMaintenance Cache => browser;
    public IMailViewSource ForParty(Party party) => _partyScopes is null ? browser.ForParty(party) : new OwnedPartyMailSource(_partyScopes, browser, party.Key);
    public async Task<byte[]> DownloadForSourceAsync(IMailViewSource source, MailMessageKey key, string attachmentId, CancellationToken cancellationToken)
    {
        await RequireSourceAsync(source, key, attachmentId, cancellationToken);
        var bytes = await DownloadAsync(key, attachmentId, cancellationToken);
        try { await RequireSourceAsync(source, key, attachmentId, cancellationToken); return bytes; }
        catch { CryptographicOperations.ZeroMemory(bytes); throw; }
    }
    private static async Task RequireSourceAsync(IMailViewSource source, MailMessageKey key, string attachment, CancellationToken token)
    {
        if (await source.GetMessageAsync(key, token) is not { } message || message.Key != key || !message.Attachments.Any(item => item.Id == attachment))
            throw new MailException(MailFailure.AccessDenied);
    }
    public async Task<byte[]> DownloadAsync(MailMessageKey key, string attachmentId, CancellationToken cancellationToken)
    {
        if (await browser.GetMessageAsync(key, cancellationToken) is not { } message || !message.Attachments.Any(item => item.Id == attachmentId)) throw new MailException(MailFailure.AccessDenied);
        if (!Guid.TryParseExact(key.Account, "N", out var id)) throw new MailException(MailFailure.AccessDenied);
        var account = await Accounts.GetAsync(id, cancellationToken); var connector = connectors.Create(account);
        if (!account.Enabled || connector.Provider != key.Provider) throw new MailException(MailFailure.AccessDenied);
        var bytes = await browser.GetAttachmentAsync(key, attachmentId, cancellationToken);
        if (bytes.Length > MailLimits.AttachmentBytes) { CryptographicOperations.ZeroMemory(bytes); throw new MailException(MailFailure.LimitExceeded); }
        return bytes;
    }
}
