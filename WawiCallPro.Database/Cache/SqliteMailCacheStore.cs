using System.Collections.Immutable;
using System.Data.Common;
using Dapper;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Infrastructure;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Processors.SQLite;
using FluentMigrator.Runner.VersionTableInfo;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Common;

namespace WawiCallPro.Database.Cache;

/// <summary>Dedicated SQLite cache. Existing communication schemas remain unchanged; all data commands use Dapper.</summary>
public sealed class SqliteMailCacheStore
{
    private readonly DatabaseTarget _target;
    private readonly DatabaseRepositoryContext _context;
    public IMailImportRepository Messages { get; }
    private SqliteMailCacheStore(DatabaseTarget target)
    { _target = target; _context = new(target, new()); Messages = new DapperMailImportRepository(_context); }

    public static async Task<SqliteMailCacheStore> OpenAsync(string path, bool create, CancellationToken token)
    {
        var target = DatabaseTarget.Sqlite(path);
        if (create)
        {
            await new DatabaseProvisioner(new()).CreateAsync(target, token);
            await new DatabaseMigrator(new(), new FluentMigratorEngine(DatabaseType.Sqlite)).MigrateAsync(target, cancellationToken: token);
            await Task.Run(() => AddBlobSchema(target), token);
        }
        await using (var db = new DatabaseFactory().CreateConnection(target, readOnly: true))
        {
            await db.OpenAsync(token);
            var owner = await db.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT id FROM mailcacheinfo", cancellationToken: token));
            if (owner != "WawiCallPro.MailCache.v1") throw new DatabaseOperationException(DatabaseErrorCode.ForeignDatabase);
        }
        if (!create)
        {
            var engine = new FluentMigratorEngine(DatabaseType.Sqlite);
            var status = await new DatabaseStatusService(new(), engine.Catalog).GetStatusAsync(target, token);
            if (status.State == DatabaseSchemaState.MigrationRequired)
                await new DatabaseMigrator(new(), engine).MigrateAsync(target, cancellationToken: token);
        }
        return new(target);
    }
    public Task<int> ReadPolicyAsync(CancellationToken token) => _context.RunAsync(false,
        async db => checked((int)(await db.OneAsync("SELECT retentiondays FROM mailcacheinfo"))!.Long("retentiondays")), token);
    public Task SetPolicyAsync(int days, CancellationToken token) => _context.RunAsync(true, async db =>
    {
        var prior = await db.OneAsync("SELECT retentiondays FROM mailcacheinfo");
        if (days > prior!.Long("retentiondays")) await db.ExecuteAsync("DELETE FROM mailsyncstate");
        return await db.ExecuteAsync("UPDATE mailcacheinfo SET retentiondays=@days", new { days });
    }, token);
    public Task PruneAsync(DateTimeOffset cutoff, CancellationToken token) => _context.RunAsync(true, async db =>
    {
        var ticks = cutoff.UtcTicks;
        const string expired = "SELECT id FROM mailmessages WHERE receivedatutc<@ticks OR hidden=1";
        await db.ExecuteAsync("DELETE FROM mailattachmentblobs WHERE messageid IN (" + expired + ")", new { ticks });
        await db.ExecuteAsync("DELETE FROM mailattachmentblobs WHERE EXISTS (SELECT 1 FROM mailmessages m WHERE m.id=mailattachmentblobs.messageid AND m.revisionhash<>mailattachmentblobs.revisionhash)");
        await db.ExecuteAsync("DELETE FROM mailaddresses WHERE messageid IN (" + expired + ")", new { ticks });
        await db.ExecuteAsync("DELETE FROM mailmemberships WHERE messageid IN (" + expired + ")", new { ticks });
        return await db.ExecuteAsync("DELETE FROM mailmessages WHERE receivedatutc<@ticks OR hidden=1", new { ticks });
    }, token);
    public Task<byte[]?> ReadBlobAsync(string message, string attachment, string revision, CancellationToken token) => _context.RunAsync(false, async db =>
    {
        var row = await db.OneAsync("SELECT payload FROM mailattachmentblobs WHERE messageid=@message AND attachmenthash=@attachment AND revisionhash=@revision", new { message, attachment, revision });
        return row?.Value("payload") as byte[];
    }, token);
    public Task WriteBlobAsync(string message, string attachment, string revision, byte[] payload, CancellationToken token) => _context.RunAsync(true, async db =>
    {
        if (payload.Length is < 1 or > WawiCallPro.Interfaces.Office.MailLimits.ProtectedAttachmentBytes) throw new ArgumentException("Invalid protected attachment size.");
        var row = await db.OneAsync("SELECT revisionhash FROM mailmessages WHERE id=@message AND hidden=0", new { message });
        if (row?.Text("revisionhash") != revision) throw new RepositoryConflictException(RepositoryConflictKind.VersionMismatch);
        await db.ExecuteAsync("DELETE FROM mailattachmentblobs WHERE messageid=@message AND attachmenthash=@attachment", new { message, attachment });
        return await db.InsertAsync("mailattachmentblobs", new() { ["messageid"] = message, ["attachmenthash"] = attachment, ["revisionhash"] = revision, ["payload"] = payload });
    }, token);

    private static void AddBlobSchema(DatabaseTarget target)
    {
        var services = new ServiceCollection();
        services.AddFluentMigratorCore().ConfigureRunner(builder => builder.AddSQLite()
            .WithGlobalConnectionString(target.ConnectionString).WithVersionTable(new CacheVersionTable()));
        services.AddScoped<SQLiteDbFactory, SqliteFactoryAdapter>();
        services.AddSingleton<IMigrationSourceItem>(new CacheMigrations());
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
    }
    private sealed class CacheMigrations : IMigrationSourceItem
    { public IEnumerable<Type> MigrationTypeCandidates => [typeof(MailCacheMigration001)]; }
    private sealed class SqliteFactoryAdapter(IServiceProvider services) : SQLiteDbFactory(services)
    { protected override DbProviderFactory CreateFactory() => SqliteFactory.Instance; }
    private sealed class CacheVersionTable : IVersionTableMetaData
    {
        public bool OwnsSchema => false; public string SchemaName => ""; public string TableName => "mailcacheversioninfo";
        public string ColumnName => "version"; public string DescriptionColumnName => "description";
        public string UniqueIndexName => "ux_mailcacheversioninfo"; public string AppliedOnColumnName => "appliedon"; public bool CreateWithPrimaryKey => false;
    }
}
