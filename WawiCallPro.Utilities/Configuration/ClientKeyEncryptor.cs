using System.Security.Cryptography;
using System.Text;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>
/// AES-128-CBC-Verschlüsselung für den Lexware API Client-Key.
/// IV (16 Bytes) wird dem Geheimtext vorangestellt und das Ergebnis Base64-kodiert.
/// </summary>
public static class ClientKeyEncryptor
{
    // 16-Byte-Schlüssel aus einem konstanten Seed ableiten (AES-128)
    private static readonly byte[] AesKey = SHA256.HashData(
        Encoding.UTF8.GetBytes("LxTranslate.LexwareClientKey.AES128.v1"))[..16];

    /// <summary>Verschlüsselt einen Klartext-Wert. Ergebnis ist Base64-kodiert (IV + Geheimtext).</summary>
    public static string Encrypt(string plaintext)
    {
        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.Key = AesKey;
        aes.GenerateIV();

        var data = Encoding.UTF8.GetBytes(plaintext);
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(data, 0, data.Length);

        var combined = new byte[16 + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, 16);
        Buffer.BlockCopy(cipher, 0, combined, 16, cipher.Length);
        return Convert.ToBase64String(combined);
    }

    /// <summary>Versucht einen Base64-kodierten AES-128-Geheimtext zu entschlüsseln.</summary>
    public static bool TryDecrypt(string encryptedBase64, out string plaintext)
    {
        plaintext = string.Empty;
        try
        {
            var data = Convert.FromBase64String(encryptedBase64);
            if (data.Length <= 16) return false;

            using var aes = Aes.Create();
            aes.KeySize = 128;
            aes.Key = AesKey;
            aes.IV = data[..16];

            var cipher = data[16..];
            using var decryptor = aes.CreateDecryptor();
            plaintext = Encoding.UTF8.GetString(decryptor.TransformFinalBlock(cipher, 0, cipher.Length));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Prüft, ob ein Wert strukturell wie ein von <see cref="Encrypt"/> erzeugter AES-CBC-Payload aussieht.
    /// Die Entschlüsselbarkeit selbst wird damit nicht bestätigt.
    /// </summary>
    public static bool LooksLikeEncryptedPayload(string? encryptedBase64)
    {
        if (string.IsNullOrWhiteSpace(encryptedBase64))
            return false;

        try
        {
            var data = Convert.FromBase64String(encryptedBase64.Trim());
            return data.Length > 16 && (data.Length - 16) % 16 == 0;
        }
        catch
        {
            return false;
        }
    }
}
