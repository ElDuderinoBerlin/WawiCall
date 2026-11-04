using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperCompanyRepository(DatabaseRepositoryContext context) : ICompanyRepository
{
    public Task<StoredCompany?> GetAsync(CompanyKey key, CancellationToken cancellationToken = default)
    {
        StorageContract.CompanyKey(key);
        return context.RunAsync(false, async db =>
        {
            var row = await RepositoryData.FindCompanyAsync(db, key).ConfigureAwait(false);
            return row is null ? null : RepositoryData.StoredCompany(row);
        }, cancellationToken);
    }
    public Task<Page<StoredCompany>> GetPageAsync(CompanyRepositoryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query); ArgumentNullException.ThrowIfNull(query.Page);
        if (query.ProductLine is { } product && !Enum.IsDefined(product)) throw new ArgumentOutOfRangeException(nameof(query));
        return context.RunAsync(false, async db =>
        {
            var sql = $"SELECT * FROM {db.T("companies")}" + (query.ProductLine is null ? "" : " WHERE productline=@product");
            var rows = await db.RowsAsync(db.PageSql(sql, "sourcehash ASC", query.Page), new { product = (int?)query.ProductLine }).ConfigureAwait(false);
            return RepositorySession.Page(rows.Select(RepositoryData.StoredCompany).ToArray(), query.Page);
        }, cancellationToken);
    }
    public Task<StoredCompany> WriteAsync(CompanyWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        return context.RunAsync(true, async db =>
        {
            var old = await RepositoryData.FindCompanyAsync(db, write.Company.Key).ConfigureAwait(false);
            RepositorySession.Expected(old, write.ExpectedVersion);
            var version = old is null ? 1 : RepositorySession.Next(old.Long("writeversion"));
            var created = old?.Utc("createdatutc") ?? write.ObservedAtUtc;
            if (old is not null) RepositoryData.Chronology(write.ObservedAtUtc, old.Utc("updatedatutc"));
            if (old is null)
                await db.InsertAsync("companies", new()
                {
                    ["id"] = DatabaseId.Encode(Guid.NewGuid()), ["sourcehash"] = SourceIdentityHash.Company(write.Company.Key),
                    ["productline"] = (int)write.Company.Key.ProductLine, ["installationid"] = write.Company.Key.InstallationId,
                    ["externalid"] = write.Company.Key.ExternalId, ["displayname"] = write.Company.DisplayName,
                    ["writeversion"] = version, ["cacheversion"] = 0L, ["createdatutc"] = DatabaseUtc.Encode(created), ["updatedatutc"] = DatabaseUtc.Encode(write.ObservedAtUtc)
                }).ConfigureAwait(false);
            else await db.UpdateAsync("companies", "id", old.Text("id"), write.ExpectedVersion!.Value,
                new() { ["displayname"] = write.Company.DisplayName, ["writeversion"] = version, ["updatedatutc"] = DatabaseUtc.Encode(write.ObservedAtUtc) }).ConfigureAwait(false);
            return new StoredCompany(write.Company, version, created, write.ObservedAtUtc);
        }, cancellationToken);
    }
}
