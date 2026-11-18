using System.Security.Cryptography;
using System.Text;
using WawiCallPro.Interfaces.Office;

namespace WawiCallPro.Office;

public interface IMailBlobProtector
{
    byte[] Protect(byte[] data, string purpose);
    byte[] Unprotect(byte[] data, string purpose);
}

/// <summary>Binary DPAPI envelopes bind attachment BLOBs to their account, message, revision and attachment ID.</summary>
public sealed class ProtectedMailBlob : IMailBlobProtector
{
    public byte[] Protect(byte[] data, string purpose)
    {
        if (!OperatingSystem.IsWindows()) throw new MailException(MailFailure.NotSupported);
        if (data.Length > MailLimits.AttachmentBytes) throw new MailException(MailFailure.LimitExceeded);
        return ProtectedData.Protect(data, Entropy(purpose), DataProtectionScope.CurrentUser);
    }
    public byte[] Unprotect(byte[] data, string purpose)
    {
        if (!OperatingSystem.IsWindows()) throw new MailException(MailFailure.NotSupported);
        try
        {
            if (data.Length > MailLimits.ProtectedAttachmentBytes) throw new CryptographicException();
            var bytes = ProtectedData.Unprotect(data, Entropy(purpose), DataProtectionScope.CurrentUser);
            if (bytes.Length > MailLimits.AttachmentBytes) throw new CryptographicException();
            return bytes;
        }
        catch (CryptographicException) { throw new MailException(MailFailure.Configuration); }
    }
    private static byte[] Entropy(string purpose) => SHA256.HashData(Encoding.UTF8.GetBytes("WawiCallPro.MailBlob.v1|" + purpose));
}
