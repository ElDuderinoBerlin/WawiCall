using System.Text.Json;
using System.Text.Json.Serialization;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Database.Repositories;

internal static class RepositoryData
{
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    internal static Company Company(RepositoryRow row)
    {
        var key = new CompanyKey((LexwareProductLine)checked((int)row.Long("productline")), row.Text("installationid"), row.Text("externalid"));
        if (SourceIdentityHash.Company(key) != row.Text("sourcehash")) Collision();
        return new Company(key, row.Text("displayname"));
    }
    internal static StoredCompany StoredCompany(RepositoryRow row) => new(Company(row), row.Long("writeversion"), row.Utc("createdatutc"), row.Utc("updatedatutc"));
    internal static async Task<RepositoryRow?> FindCompanyAsync(RepositorySession db, CompanyKey key)
    {
        var row = await db.OneAsync($"SELECT * FROM {db.T("companies")} WHERE sourcehash=@hash", new { hash = SourceIdentityHash.Company(key) }).ConfigureAwait(false);
        if (row is not null && Company(row).Key != key) Collision();
        return row;
    }
    internal static async Task<RepositoryRow> RequireCompanyAsync(RepositorySession db, CompanyKey key) =>
        await FindCompanyAsync(db, key).ConfigureAwait(false) ?? throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
    internal static async Task<Company> CompanyByIdAsync(RepositorySession db, string id)
    {
        var row = await db.OneAsync($"SELECT * FROM {db.T("companies")} WHERE id=@id", new { id }).ConfigureAwait(false)
            ?? throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema);
        return Company(row);
    }
    internal static PartyKey PartyKey(RepositoryRow row, CompanyKey company, string kind = "kind") =>
        new(company, (PartyKind)checked((int)row.Long(kind)), row.Text("externalid"), kind == "partykind" ? null : row.OptionalText("ownerexternalid"));
    internal static void Collision() => throw new RepositoryConflictException(RepositoryConflictKind.IdentityCollision);
    internal static void Chronology(DateTimeOffset changed, DateTimeOffset previous)
    { if (changed < previous) throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch); }

    internal static void Bounds(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from is { } start) StorageContract.Utc(start);
        if (to is { } end) StorageContract.Utc(end);
        if (from > to) throw new ArgumentException("Invalid UTC interval.");
    }

    internal static string? EncodeMatch(PhoneNumberMatchEvidence? match)
    {
        if (match is null) return null;
        StorageContract.Match(match);
        var value = JsonSerializer.Serialize(new MatchPayload(1, match), Json);
        StorageContract.Text(value, 8192, nameof(match), required: true);
        return value;
    }
    internal static PhoneNumberMatchEvidence? DecodeMatch(RepositoryRow row)
    {
        var value = row.OptionalText("evidencejson");
        if (value is null)
        {
            if (row.Long("matchkind") != 0) throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema);
            return null;
        }
        var payload = JsonSerializer.Deserialize<MatchPayload>(value, Json) ?? throw new JsonException();
        if (payload.FormatVersion != 1) throw new JsonException();
        StorageContract.Match(payload.Match);
        if ((int)payload.Match.Kind != row.Long("matchkind") || payload.Match.StartWidth != row.OptionalLong("startwidth") || payload.Match.ApparatusNumber != row.OptionalText("apparatusnumber"))
            throw new JsonException();
        return payload.Match;
    }
    internal static string? EncodeLookup(CallerLookupSnapshot? lookup)
    {
        if (lookup is null) return null;
        StorageContract.Lookup(lookup);
        var value = JsonSerializer.Serialize(lookup, Json);
        StorageContract.Text(value, 1048576, nameof(lookup), required: true);
        return value;
    }
    internal static CallerLookupSnapshot DecodeLookup(RepositoryRow row)
    {
        if (row.Long("formatversion") != 1) throw new JsonException();
        var lookup = JsonSerializer.Deserialize<CallerLookupSnapshot>(row.Text("detailsjson"), Json) ?? throw new JsonException();
        StorageContract.Lookup(lookup);
        if (lookup.ParserVersion != row.Text("parserversion") || lookup.IsComplete != row.Bool("iscomplete") || lookup.SearchedAtUtc != row.Utc("searchedatutc") || lookup.SelectionReason != row.OptionalText("selectionreason"))
            throw new JsonException();
        return lookup;
    }
    private sealed record MatchPayload(int FormatVersion, PhoneNumberMatchEvidence Match);
}
