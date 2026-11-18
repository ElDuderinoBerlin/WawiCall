using System.Security.Cryptography;
using System.Text.Json;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Office;

/// <summary>Detects changes to read access across provider awaits without treating display labels as permissions.</summary>
internal static class MailAccountScope
{
    internal static string Key(MailAccountSettings account)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            account.Id, account.Enabled, account.Provider, account.ClientId, account.Tenant,
            account.ApplicationAuthentication, account.Mailbox, account.RemoteIdentity, account.OutlookStoreId,
            account.Company, account.ImportDays, account.MaximumMessages,
            Folders = account.FolderIds.Order(StringComparer.Ordinal).ToArray(),
            account.MicrosoftClientSecret, account.GoogleClientSecret
        });
        try { return Convert.ToHexString(SHA256.HashData(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static void Verify(MailAccountSettings expected, IEnumerable<MailAccountSettings> current)
    {
        var actual = current.SingleOrDefault(item => item.Id == expected.Id);
        if (actual is null || !actual.Enabled || Key(actual) != Key(expected)) throw new MailException(MailFailure.Conflict);
    }

    internal static async Task VerifyAsync(MailAccounts accounts, MailAccountSettings expected, CancellationToken token)
    { token.ThrowIfCancellationRequested(); Verify(expected, await accounts.GetAsync(token)); }
}
