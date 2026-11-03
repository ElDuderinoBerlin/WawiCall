using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WawiCallPro.Utilities.Security;

/// <summary>
/// Short-lived plaintext buffer. Reveal only at the external API boundary and dispose after use.
/// Strings returned by Reveal cannot be erased by this owner; no plaintext is exposed as a property.
/// </summary>
[JsonConverter(typeof(SecretValueJsonConverter))]
public sealed class SecretValue : IDisposable
{
    private readonly byte[] _utf8;
    private bool _disposed;

    internal SecretValue(byte[] utf8) => _utf8 = utf8;

    [JsonIgnore]
    public bool IsEmpty => _utf8.Length == 0;

    public string Reveal()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Encoding.UTF8.GetString(_utf8);
    }

    public override string ToString() => "[redacted]";

    public void Dispose()
    {
        if (_disposed) return;
        CryptographicOperations.ZeroMemory(_utf8);
        _disposed = true;
    }
}

public sealed class SecretValueJsonConverter : JsonConverter<SecretValue>
{
    public override SecretValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new JsonException("Plaintext secrets cannot be deserialized.");

    public override void Write(Utf8JsonWriter writer, SecretValue value, JsonSerializerOptions options) =>
        writer.WriteStringValue("[redacted]");
}
