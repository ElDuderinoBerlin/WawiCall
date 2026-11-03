using System.Security.Cryptography;
using System.Text;

namespace WawiCallPro.Utilities.Security;

public interface ISecretProtector
{
    ProtectedSecret Protect(string plaintext, string purpose);
    SecretValue Unprotect(ProtectedSecret secret, string purpose);
}

/// <summary>Uniform, non-sensitive failure; native exception details and supplied values are not retained.</summary>
public sealed class SecretProtectionException : Exception
{
    internal SecretProtectionException() : base("Der geschützte Wert ist für dieses Benutzerkonto nicht verfügbar oder beschädigt.") { }
}

/// <summary>Windows DPAPI CurrentUser with application/purpose binding; there is no plaintext or machine-wide fallback.</summary>
public sealed class WindowsUserSecretProtector : ISecretProtector
{
    public ProtectedSecret Protect(string plaintext, string purpose)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        if (Encoding.UTF8.GetByteCount(plaintext) > 16384)
            throw new ArgumentException("The secret exceeds the supported size.", nameof(plaintext));
        var entropy = CreateEntropy(purpose);
        if (!OperatingSystem.IsWindows()) throw new SecretProtectionException();
        var utf8 = Encoding.UTF8.GetBytes(plaintext);
        try
        {
            var encrypted = ProtectedData.Protect(utf8, entropy, DataProtectionScope.CurrentUser);
            return new ProtectedSecret(ProtectedSecret.Prefix + Convert.ToBase64String(encrypted));
        }
        catch (CryptographicException)
        {
            throw new SecretProtectionException();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(utf8);
        }
    }

    public SecretValue Unprotect(ProtectedSecret secret, string purpose)
    {
        ArgumentNullException.ThrowIfNull(secret);
        var entropy = CreateEntropy(purpose);
        if (!OperatingSystem.IsWindows()) throw new SecretProtectionException();
        try
        {
            var encrypted = Convert.FromBase64String(secret.Envelope[ProtectedSecret.Prefix.Length..]);
            return new SecretValue(ProtectedData.Unprotect(encrypted, entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            throw new SecretProtectionException();
        }
    }

    private static byte[] CreateEntropy(string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (purpose.Length > 128 || purpose.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
            throw new ArgumentException("The secret purpose is invalid.", nameof(purpose));
        return Encoding.UTF8.GetBytes("WawiCallPro/credentials/v1/" + purpose);
    }
}
