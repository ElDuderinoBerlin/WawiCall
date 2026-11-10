using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>The writer and search index use exactly the same normalization/context identity.</summary>
public interface IPhoneCacheMetadata
{
    string ParserVersion { get; }
    string GetContextVersion(CompanySettings company);
}

public sealed class PhoneCacheMetadata(IPhoneNumberParser parser, IPhoneNumberInterpreter interpreter) : IPhoneCacheMetadata
{
    public string ParserVersion => parser.Version;
    public string GetContextVersion(CompanySettings company)
    {
        var interpretation = interpreter.InterpretIncoming(new(NumberPresentation.Unknown)).Version;
        var content = JsonSerializer.Serialize(new { Protocol = "phone-cache/1", Interpretation = interpretation, company.Company, company.DefaultPhoneRegion, company.LocalAreaCode });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
    public static bool IsContextFingerprint(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    public static bool SameDatabase(CommunicationDatabaseSettings first, CommunicationDatabaseSettings second) => first.Provider == second.Provider &&
        (first.Provider == CommunicationDatabaseProvider.Sqlite
            ? string.Equals(first.SqliteFileName ?? "communications.db", second.SqliteFileName ?? "communications.db", StringComparison.Ordinal)
            : JsonSerializer.Serialize(first.ConnectionString) == JsonSerializer.Serialize(second.ConnectionString));
}
