using System.Data.Common;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class ProviderCompatibilityTests
{
    [TestMethod]
    public async Task SqliteFreshSchemaAndPopulatedVersionSevenUpgradePreserveSourceData()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WawiCallPro-provider-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { await ProviderCompatibilityAcceptance.RunAsync(DatabaseTarget.Sqlite(Path.Combine(directory, "source.db"))); }
        finally { Directory.Delete(directory, true); }
    }
}

/// <summary>The same fresh-install and populated-upgrade assertions run against each disposable native engine.</summary>
public static class ProviderCompatibilityAcceptance
{
    public static async Task RunAsync(DatabaseTarget isolatedSource)
    {
        var factory = new DatabaseFactory();
        var engine = new FluentMigratorEngine(isolatedSource.Type);
        if (isolatedSource.Type == DatabaseType.MySql)
        {
            await VerifyActiveRolesAsync(isolatedSource);
            Console.WriteLine("MySQL/MariaDB: direct read permission, inactive role rejection and active role DDL permission passed.");
        }
        foreach (var upgrade in new[] { false, true })
        {
            var target = NewTarget(isolatedSource);
            var receipt = await new DatabaseProvisioner(factory).CreateAsync(target);
            var company = new CompanyKey(LexwareProductLine.Pro, "Source Ä 电话", Guid.NewGuid().ToString("N"));
            var key = new PartyKey(company, PartyKind.Customer, "0001");
            var companyId = DatabaseId.Encode(Guid.NewGuid());
            var partyId = DatabaseId.Encode(Guid.NewGuid());
            var addressId = DatabaseId.Encode(Guid.NewGuid());
            if (upgrade)
            {
                for (long version = 1; version <= 7; version++)
                {
                    engine.MigrateUp(target, version);
                    var state = await new DatabaseStatusService(factory, engine.Catalog).GetStatusAsync(target);
                    CollectionAssert.AreEqual(Enumerable.Range(1, (int)version).Select(number => (long)number).ToArray(), state.AppliedVersions.ToArray());
                }
                await using var db = factory.CreateConnection(target);
                await db.OpenAsync();
                await InsertAsync(db, target.Type, "companies", new()
                {
                    ["id"] = companyId, ["sourcehash"] = SourceIdentityHash.Company(company), ["productline"] = 1,
                    ["installationid"] = company.InstallationId, ["externalid"] = company.ExternalId, ["displayname"] = "Upgrade Ä 电话",
                    ["writeversion"] = 1L, ["cacheversion"] = 0L,
                    ["createdatutc"] = RepositoryAcceptance.Time.Ticks, ["updatedatutc"] = RepositoryAcceptance.Time.Ticks
                });
                await InsertAsync(db, target.Type, "parties", new()
                {
                    ["id"] = partyId, ["companyid"] = companyId, ["sourcehash"] = SourceIdentityHash.Party(key),
                    ["kind"] = 1, ["externalid"] = key.ExternalId, ["displayname"] = "Legacy Ä 电话", ["isinactive"] = 0,
                    ["refreshedatutc"] = RepositoryAcceptance.Time.Ticks, ["searchname"] = "LEGACY Ä 电话"
                });
                await InsertAsync(db, target.Type, "partyaddresses", new()
                {
                    ["id"] = addressId, ["partyid"] = partyId, ["sourcehash"] = SourceIdentityHash.Address(key, AddressKind.Billing, "0001"),
                    ["externalid"] = "0001", ["kind"] = 1, ["position"] = 0, ["street"] = "Straße 电话", ["postalcode"] = "00123"
                });
                await InsertAsync(db, target.Type, "partyphones", new()
                {
                    ["id"] = DatabaseId.Encode(Guid.NewGuid()), ["partyid"] = partyId, ["addressid"] = addressId,
                    ["position"] = 0, ["kind"] = 1, ["originalvalue"] = "030 / 123456", ["e164"] = "+4930123456",
                    ["sourcerecordtype"] = "Customer", ["sourcerecordid"] = "0001", ["sourcefieldname"] = "Telephone"
                });
            }

            var migrator = new DatabaseMigrator(factory, engine);
            var migrated = await migrator.MigrateAsync(target, creation: receipt);
            Assert.AreEqual(DatabaseSchemaState.Current, migrated.State);
            CollectionAssert.AreEqual(engine.Catalog.Versions.ToArray(), migrated.AppliedVersions.ToArray());
            var context = new DatabaseRepositoryContext(target, factory);
            var cache = new DapperPartyCacheRepository(context);
            if (upgrade)
            {
                var legacy = (await cache.GetAsync(key))!;
                Assert.AreEqual("Legacy Ä 电话", legacy.DisplayName);
                Assert.IsNull(legacy.Source);
                Assert.IsNull(legacy.Addresses.Single().Source);
                Assert.IsNull(legacy.Addresses[0].Country);
                Assert.AreEqual("00123", legacy.Addresses[0].PostalCode);
                Assert.AreEqual("030 / 123456", legacy.Addresses[0].Phones.Single().OriginalValue);
                await using var db = factory.CreateConnection(target);
                await db.OpenAsync();
                Assert.AreEqual(partyId, await ScalarAsync(db, "SELECT id FROM " + DatabaseSql.Table(target.Type, "parties")));
                Assert.AreEqual(addressId, await ScalarAsync(db, "SELECT id FROM " + DatabaseSql.Table(target.Type, "partyaddresses")));
            }
            else await new DapperCompanyRepository(context).WriteAsync(new(new(company, "Fresh Ä 电话"), null, RepositoryAcceptance.Time));

            var phone = new PartyPhone("030 / 123456", PhoneKind.Telephone, new("fk_kunde", "0001", "Telephone"), "+4930123456");
            var address = new PartyAddress
            {
                ExternalId = "0001", Kind = AddressKind.Billing, Source = new(new string('界', 64), new string('Ä', 256), new string('源', 128)),
                Country = new string('国', 512), PostalCode = "00123", Street = "Straße 电话 😀", Phones = [phone]
            };
            var party = new Party
            {
                Key = key, DisplayName = "Neu Ä 电话 😀", Source = new(new string('源', 128), new string('Ä', 256)), Addresses = [address]
            };
            await cache.ReplaceAsync(new(company, 0, [party], RepositoryAcceptance.Time));
            var reloaded = (await cache.GetAsync(key))!;
            Assert.AreEqual(party.Source, reloaded.Source);
            Assert.AreEqual(party.DisplayName, reloaded.DisplayName);
            Assert.AreEqual(address.Source, reloaded.Addresses.Single().Source);
            Assert.AreEqual(address.Country, reloaded.Addresses[0].Country);
            Assert.AreEqual(address.Street, reloaded.Addresses[0].Street);
            Assert.AreEqual(phone, reloaded.Addresses[0].Phones.Single());
            Assert.AreEqual(key, (await cache.GetPageAsync(new(company, new PageRequest(), "Ä 电话"))).Page.Items.Single().Key);
            Assert.AreEqual(key, (await cache.FindByE164Async(company, "+4930123456", new PageRequest())).Page.Items.Single().Key);

            await using (var db = factory.CreateConnection(target))
            {
                await db.OpenAsync();
                foreach (var (table, column, maximum) in new[]
                {
                    ("parties", "sourcerecordtype", 128), ("parties", "sourcerecordid", 256),
                    ("partyaddresses", "sourcecountry", 512), ("partyaddresses", "sourcerecordtype", 128),
                    ("partyaddresses", "sourcerecordid", 256), ("partyaddresses", "sourcefield", 256)
                })
                    await Assert.ThrowsAsync<DbException>(() => ExecuteAsync(db,
                        $"UPDATE {DatabaseSql.Table(target.Type, table)} SET {Quote(target.Type, column)}=@value", new string('x', maximum + 1)));
                await Assert.ThrowsAsync<DbException>(() => ExecuteAsync(db,
                    "UPDATE " + DatabaseSql.Table(target.Type, "parties") + " SET sourcehash=@value", new string('A', 64)));
                await Assert.ThrowsAsync<DbException>(() => ExecuteAsync(db,
                    "UPDATE " + DatabaseSql.Table(target.Type, "partyphones") + " SET e164=@value", "+49١٢٣"));
            }
            var before = await ServerBackupAcceptance.SnapshotAsync(target);
            Assert.AreEqual(DatabaseSchemaState.Current, (await migrator.MigrateAsync(target)).State);
            CollectionAssert.AreEqual(before, await ServerBackupAcceptance.SnapshotAsync(target));
            await cache.ReplaceAsync(new(company, 1, [party with { Source = null, Addresses = [address with { Source = null, Country = null }] }], RepositoryAcceptance.Time.AddTicks(1)));
            var cleared = (await cache.GetAsync(key))!;
            Assert.IsNull(cleared.Source);
            Assert.IsNull(cleared.Addresses[0].Source);
            Assert.IsNull(cleared.Addresses[0].Country);
        }
    }

    private static async Task VerifyActiveRolesAsync(DatabaseTarget target)
    {
        await using var admin = new DatabaseFactory().CreateConnection(target);
        await admin.OpenAsync();
        var mariaDb = admin.ServerVersion.Contains("MariaDB", StringComparison.OrdinalIgnoreCase);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var userName = "wcp_" + suffix;
        var user = "'" + userName + "'@'127.0.0.1'";
        var role = mariaDb ? "`wcp_role_" + suffix + "`" : "'wcp_role_" + suffix + "'@'%'";
        var password = "Synthetic" + Guid.NewGuid().ToString("N");
        var userCreated = false;
        var roleCreated = false;
        try
        {
            await StatementAsync(admin, "CREATE USER " + user + " IDENTIFIED BY '" + password + "'");
            userCreated = true;
            await StatementAsync(admin, "GRANT SELECT ON " + Quote(target.Type, target.DatabaseName) + ".* TO " + user);
            await StatementAsync(admin, "CREATE ROLE " + role);
            roleCreated = true;
            await StatementAsync(admin, "GRANT ALL PRIVILEGES ON " + Quote(target.Type, target.DatabaseName) + ".* TO " + role);
            await StatementAsync(admin, "GRANT " + role + " TO " + user);
            var options = new MySqlConnectionStringBuilder(target.ConnectionString) { UserID = userName, Password = password, Pooling = false };
            await using var limited = new MySqlConnection(options.ConnectionString);
            await limited.OpenAsync();
            var permissions = new DatabaseMigrationPermissions(new());
            await StatementAsync(limited, "SET ROLE NONE");
            Assert.AreEqual(DatabaseErrorCode.AccessDenied,
                (await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => permissions.EnsureAsync(limited, target))).Code);
            await StatementAsync(limited, "SET ROLE " + role);
            await permissions.EnsureAsync(limited, target);
            await StatementAsync(limited, "SET ROLE NONE");
            Assert.AreEqual(DatabaseErrorCode.AccessDenied,
                (await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => permissions.EnsureAsync(limited, target))).Code);
        }
        finally
        {
            if (userCreated) await StatementAsync(admin, "DROP USER " + user);
            if (roleCreated) await StatementAsync(admin, "DROP ROLE " + role);
        }
    }

    private static async Task StatementAsync(DbConnection db, string sql)
    {
        using var command = db.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync();
    }

    private static DatabaseTarget NewTarget(DatabaseTarget source)
    {
        var name = "wcp_sources_" + Guid.NewGuid().ToString("N");
        return source.Type switch
        {
            DatabaseType.Sqlite => DatabaseTarget.Sqlite(Path.Combine(Path.GetDirectoryName(source.SqlitePath!)!, name + ".db")),
            DatabaseType.SqlServer => DatabaseTarget.Server(source.Type, new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = name, Pooling = false }.ConnectionString),
            DatabaseType.MySql => DatabaseTarget.Server(source.Type, new MySqlConnectionStringBuilder(source.ConnectionString) { Database = name, Pooling = false }.ConnectionString),
            _ => DatabaseTarget.Server(source.Type, new NpgsqlConnectionStringBuilder(source.ConnectionString) { Database = name, Pooling = false }.ConnectionString)
        };
    }

    private static string Quote(DatabaseType type, string name) => type switch
    {
        DatabaseType.SqlServer => "[" + name + "]", DatabaseType.MySql => "`" + name + "`", _ => "\"" + name + "\""
    };

    private static async Task InsertAsync(DbConnection db, DatabaseType type, string table, Dictionary<string, object?> values)
    {
        using var command = db.CreateCommand();
        command.CommandText = $"INSERT INTO {DatabaseSql.Table(type, table)} ({string.Join(',', values.Keys.Select(name => Quote(type, name)))}) VALUES ({string.Join(',', values.Keys.Select((_, index) => "@p" + index))})";
        foreach (var (value, index) in values.Values.Select((value, index) => (value, index))) Add(command, "p" + index, value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(DbConnection db, string sql, object value)
    {
        using var command = db.CreateCommand(); command.CommandText = sql; Add(command, "value", value); await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(DbConnection db, string sql)
    {
        using var command = db.CreateCommand(); command.CommandText = sql; return await command.ExecuteScalarAsync();
    }

    private static void Add(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value ?? DBNull.Value; command.Parameters.Add(parameter);
    }
}
