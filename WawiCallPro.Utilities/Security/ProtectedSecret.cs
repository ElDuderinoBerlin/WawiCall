using System.Text.Json;
using System.Text.Json.Serialization;

namespace WawiCallPro.Utilities.Security;

/// <summary>Persistable ciphertext, never plaintext. Its diagnostic representation deliberately omits its payload.</summary>
[JsonConverter(typeof(ProtectedSecretJsonConverter))]
public sealed class ProtectedSecret
{
    internal const string Prefix = "dpapi-user-v1:";
    internal string Envelope { get; }

    internal ProtectedSecret(string envelope)
    {
        Envelope = envelope;
    }

    public override string ToString() => "[protected]";
}

public sealed class ProtectedSecretJsonConverter : JsonConverter<ProtectedSecret>
{
    public override ProtectedSecret Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("A protected value is required.");
        var envelope = reader.GetString()!;
        if (envelope.Length > 65536 || !envelope.StartsWith(ProtectedSecret.Prefix, StringComparison.Ordinal))
            throw new JsonException("The protected value format is invalid.");
        try
        {
            var encrypted = Convert.FromBase64String(envelope[ProtectedSecret.Prefix.Length..]);
            if (encrypted.Length == 0) throw new JsonException("The protected value is empty.");
        }
        catch (FormatException)
        {
            throw new JsonException("The protected value format is invalid.");
        }
        return new ProtectedSecret(envelope);
    }

    public override void Write(Utf8JsonWriter writer, ProtectedSecret value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Envelope);
}
