using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Database.Contracts;

/// <summary>Canonical lowercase GUIDs give all providers the same ordinal key ordering.</summary>
public static class DatabaseId
{
    public static string Encode(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("A database ID must not be empty.", nameof(value));
        return value.ToString("N");
    }

    public static Guid Decode(string value)
    {
        if (!Guid.TryParseExact(value, "N", out var id) || id == Guid.Empty || value != id.ToString("N"))
            throw new ArgumentException("A database ID must be a canonical lowercase GUID.", nameof(value));
        return id;
    }
}

/// <summary>Signed 64-bit UTC ticks preserve 100-nanosecond precision across all four providers.</summary>
public static class DatabaseUtc
{
    /// <summary>Converts the actual instant, including a nonzero source offset, without relabelling local clock time.</summary>
    public static long Encode(DateTimeOffset value) => value.UtcTicks;

    public static DateTimeOffset Decode(long ticks) => new(ticks, TimeSpan.Zero);
}

/// <summary>
/// Versioned, length-prefixed UTF-8 identities. Repositories must compare the original identity after a hash lookup
/// and reject a collision; a digest is an index key, never permission to merge different source records.
/// </summary>
public static class SourceIdentityHash
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static string Company(CompanyKey key)
    {
        StorageContract.CompanyKey(key);
        return Hash("WawiCallPro/company/v1", ((int)key.ProductLine).ToString(CultureInfo.InvariantCulture), key.InstallationId, key.ExternalId);
    }

    public static string Party(PartyKey key)
    {
        StorageContract.PartyKey(key);
        return Hash("WawiCallPro/party/v1", Company(key.Company), ((int)key.Kind).ToString(CultureInfo.InvariantCulture), key.OwnerExternalId, key.ExternalId);
    }

    public static string Address(PartyKey party, AddressKind kind, string externalId)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        StorageContract.Text(externalId, 256, nameof(externalId), required: true);
        return Hash("WawiCallPro/address/v1", Party(party), ((int)kind).ToString(CultureInfo.InvariantCulture), externalId);
    }

    private static string Hash(params string?[] components)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        foreach (var component in components)
        {
            var bytes = component is null ? null : Utf8.GetBytes(component);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes?.Length ?? -1);
            hash.AppendData(length);
            if (bytes is not null) hash.AppendData(bytes);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
