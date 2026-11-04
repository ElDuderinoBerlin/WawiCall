using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperNumberParseLogRepository(DatabaseRepositoryContext context) : INumberParseLogRepository
{
    public async Task AppendAsync(NumberParseFailure failure, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(failure);
        await context.RunAsync(true, async db =>
        {
            var company = await RepositoryData.RequireCompanyAsync(db, failure.Party.Company).ConfigureAwait(false);
            var id = DatabaseId.Encode(failure.Id);
            var row = await db.OneAsync($"SELECT * FROM {db.T("numberparselogs")} WHERE id=@id", new { id }).ConfigureAwait(false);
            if (row is not null)
            {
                if (await ReadAsync(db, row).ConfigureAwait(false) != failure) throw new RepositoryConflictException(RepositoryConflictKind.AppendConflict);
                return true;
            }
            await db.InsertAsync("numberparselogs", new()
            {
                ["id"] = id, ["companyid"] = company.Text("id"), ["partykeyhash"] = SourceIdentityHash.Party(failure.Party),
                ["kind"] = (int)failure.Party.Kind, ["externalid"] = failure.Party.ExternalId, ["ownerexternalid"] = failure.Party.OwnerExternalId,
                ["phonekind"] = (int)failure.Kind, ["sourcerecordtype"] = failure.Source.RecordType, ["sourcerecordid"] = failure.Source.RecordId,
                ["sourcefieldname"] = failure.Source.FieldName, ["originalvalue"] = failure.OriginalValue, ["code"] = failure.Code, ["occurredatutc"] = DatabaseUtc.Encode(failure.OccurredAtUtc)
            }).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<Page<NumberParseFailure>> GetPageAsync(NumberParseLogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query); StorageContract.CompanyKey(query.Company); ArgumentNullException.ThrowIfNull(query.Page);
        RepositoryData.Bounds(query.FromUtc, query.ToUtc);
        return context.RunAsync(false, async db =>
        {
            var company = await RepositoryData.RequireCompanyAsync(db, query.Company).ConfigureAwait(false);
            var sql = $"SELECT * FROM {db.T("numberparselogs")} WHERE companyid=@company" +
                (query.FromUtc is null ? "" : " AND occurredatutc>=@from") + (query.ToUtc is null ? "" : " AND occurredatutc<@to");
            var rows = await db.RowsAsync(db.PageSql(sql, "occurredatutc DESC,id ASC", query.Page),
                new { company = company.Text("id"), from = query.FromUtc?.Ticks, to = query.ToUtc?.Ticks }).ConfigureAwait(false);
            var result = new List<NumberParseFailure>();
            foreach (var row in rows) result.Add(await ReadAsync(db, row).ConfigureAwait(false));
            return RepositorySession.Page(result, query.Page);
        }, cancellationToken);
    }

    private static async Task<NumberParseFailure> ReadAsync(RepositorySession db, RepositoryRow row)
    {
        var company = await RepositoryData.CompanyByIdAsync(db, row.Text("companyid")).ConfigureAwait(false);
        var key = RepositoryData.PartyKey(row, company.Key);
        if (SourceIdentityHash.Party(key) != row.Text("partykeyhash")) RepositoryData.Collision();
        return new(row.Id("id"), key, new(row.Text("sourcerecordtype"), row.Text("sourcerecordid"), row.Text("sourcefieldname")),
            (WawiCallPro.Interfaces.Parties.PhoneKind)checked((int)row.Long("phonekind")), row.Text("originalvalue"), row.Text("code"), row.Utc("occurredatutc"));
    }
}
