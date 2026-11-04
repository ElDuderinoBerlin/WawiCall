using System.Data.Common;
using System.Security.Cryptography;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class ProductSchemaTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-schema-" + Guid.NewGuid().ToString("N"));
    private const long Time = 638000000000000001;
    private static string Id(char value) => new(value, 32);
    private static string Hash(char value) => new(value, 64);

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [TestMethod]
    public async Task EmptyDatabaseMigratesToTheExactProductSchemaAndRepeatedRunsDoNotWrite()
    {
        var target = Target();
        await new DatabaseProvisioner(new()).CreateAsync(target);
        var engine = new FluentMigratorEngine(DatabaseType.Sqlite);
        var migrator = new DatabaseMigrator(new(), engine);
        var status = await migrator.MigrateAsync(target);
        Assert.AreEqual(DatabaseSchemaState.Current, status.State);
        CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4, 5, 6 }, status.AppliedVersions.ToArray());
        using (var db = Open(target))
        {
            Assert.AreEqual("callevents,calllookups,callnotes,callparties,companies,numberparselogs,parties,partyaddresses,partyphones,phonecalls,phonematchsettings,phonenumberblockrules,recordings,versioninfo,wawicallpro_metadata",
                Scalar(db, "SELECT group_concat(name,',') FROM (SELECT name FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name)"));
            Assert.AreEqual("WawiCallPro.Communications", Scalar(db, "SELECT applicationid FROM wawicallpro_metadata"));
            Assert.AreEqual(1L, Scalar(db, "SELECT formatversion FROM wawicallpro_metadata"));
            Assert.IsTrue((long)Scalar(db, "SELECT createdatutc FROM wawicallpro_metadata")! > Time);
            Assert.AreEqual(0L, Scalar(db, "SELECT COUNT(*) FROM companies"));
            Assert.AreEqual(0L, Scalar(db, "SELECT COUNT(*) FROM pragma_foreign_key_check"));
            Assert.AreEqual("ok", Scalar(db, "PRAGMA integrity_check"));
            Assert.AreEqual(1L, Scalar(db, "SELECT pk FROM pragma_table_info('wawicallpro_metadata') WHERE name='applicationid'"));
        }
        var before = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(target.SqlitePath!)));
        Assert.AreEqual(DatabaseSchemaState.Current, (await migrator.MigrateAsync(target)).State);
        Assert.AreEqual(before, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(target.SqlitePath!))));
    }

    [TestMethod]
    public async Task EachVersionAddsOnlyItsTablesAndUpgradePreservesExistingData()
    {
        var target = Target();
        await new DatabaseProvisioner(new()).CreateAsync(target);
        var engine = new FluentMigratorEngine(DatabaseType.Sqlite);
        int[] tableCounts = [3, 6, 10, 11, 12, 15];
        for (var version = 1; version <= 6; version++)
        {
            engine.MigrateUp(target, version);
            using var db = Open(target);
            Assert.AreEqual((long)tableCounts[version - 1], Scalar(db, "SELECT COUNT(*) FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%'"));
            if (version == 1) Insert(db, "companies", Company());
            Assert.AreEqual("Mandant Ä 电话 ' 001", Scalar(db, "SELECT displayname FROM companies"));
            Assert.AreEqual(Time + 1, Scalar(db, "SELECT updatedatutc FROM companies"));
            var status = await new DatabaseStatusService(new(), engine.Catalog).GetStatusAsync(target);
            Assert.AreEqual(version == engine.Catalog.LatestVersion ? DatabaseSchemaState.Current : DatabaseSchemaState.MigrationRequired, status.State);
            CollectionAssert.AreEqual(Enumerable.Range(1, version).Select(value => (long)value).ToArray(), status.AppliedVersions.ToArray());
        }
    }

    [TestMethod]
    public async Task CompanyKeysCountersCacheStateUnicodeAndExactTicksAreEnforced()
    {
        using var db = await Database();
        Insert(db, "companies", Company());
        var invalid = new (string Column, object? Value)[]
        {
            ("id", new string('A', 32)), ("id", new string('0', 32)), ("id", Id('1') + "\0"),
            ("sourcehash", Hash('A')), ("sourcehash", "short"), ("productline", 3), ("writeversion", 0),
            ("cacheversion", -1), ("cacheversion", 1), ("cacherefreshedatutc", Time),
            ("displayname", "\t\r\n\u00a0\u3000"), ("displayname", new string('x', 513)),
            ("externalid", ""), ("externalid", "x\0y"), ("createdatutc", -1),
            ("updatedatutc", Time - 1), ("updatedatutc", 3155378976000000000L), ("updatedatutc", 0.5)
        };
        foreach (var (column, value) in invalid) RejectUpdate(db, "companies", column, value);
        Assert.AreEqual(Time + 1, Scalar(db, "SELECT updatedatutc FROM companies"));
        Assert.AreEqual("0001", Scalar(db, "SELECT externalid FROM companies"));
        Execute(db, "UPDATE companies SET cacheversion=1,cacherefreshedatutc=@value", Time + 2);
        Assert.AreEqual(Time + 2, Scalar(db, "SELECT cacherefreshedatutc FROM companies"));
        RejectInsert(db, "companies", Company('2', 'a'));
        RejectUpdate(db, "wawicallpro_metadata", "applicationid", "wawicallpro.communications");
    }

    [TestMethod]
    public async Task CacheAllowsSharedNumbersButRejectsCrossOwnerAddressesAndInvalidE164()
    {
        using var db = await Database();
        Insert(db, "companies", Company());
        Insert(db, "companies", Company('2', 'b'));
        Insert(db, "parties", Party('3', '1', 'c'));
        Insert(db, "parties", Party('4', '2', 'd'));
        Insert(db, "partyaddresses", Address());
        Insert(db, "partyphones", Phone('6', '3', '5'));
        Insert(db, "partyphones", Phone('7', '4'));
        Assert.AreEqual(2L, Scalar(db, "SELECT COUNT(*) FROM partyphones WHERE e164='+4930123456'"));
        Assert.AreEqual("00123", Scalar(db, "SELECT postalcode FROM partyaddresses"));
        RejectInsert(db, "partyphones", Phone('8', '4', '5', 1));
        RejectInsert(db, "partyphones", Phone('8', '9', position: 1));
        RejectUpdate(db, "parties", "kind", 3);
        RejectUpdate(db, "parties", "ownerexternalid", "unexpected");
        foreach (var value in new[] { "+1", "+0123", "+1234567890123456", "+12a", "+12 ", "49123", "+١٢" })
            RejectUpdate(db, "partyphones", "e164", value);
        RejectUpdate(db, "partyphones", "kind", 4);
        RejectUpdate(db, "partyphones", "position", -1);
        Assert.Throws<DbException>(() => Execute(db, "DELETE FROM companies"));
        Assert.Throws<DbException>(() => Execute(db, "DELETE FROM parties"));
        Assert.Throws<DbException>(() => Execute(db, "DELETE FROM partyaddresses"));
    }

    [TestMethod]
    public async Task HistoricalCandidatesSurviveCacheDeletionAndOnlyOneCandidateCanBeSelected()
    {
        using var db = await Database();
        SeedCall(db);
        Insert(db, "parties", Party('3', '1', 'c'));
        Insert(db, "callparties", Candidate('4', 'c', 0, 1));
        Insert(db, "callparties", Candidate('5', 'd', 1, 0));
        Insert(db, "callparties", Candidate('6', 'e', 2, 0));
        RejectInsert(db, "callparties", Candidate('7', 'f', 3, 1));
        RejectInsert(db, "callparties", Candidate('8', 'c', 3, 0));
        Execute(db, "DELETE FROM parties");
        Assert.AreEqual(3L, Scalar(db, "SELECT COUNT(*) FROM callparties"));
        Assert.AreEqual("Historisch Ä 电话", Scalar(db, "SELECT displayname FROM callparties LIMIT 1"));
        Assert.Throws<DbException>(() => Execute(db, "DELETE FROM phonecalls"));
    }

    [TestMethod]
    public async Task CallSnapshotChecksEnforceDirectionPresentationTimelineAndEndReasons()
    {
        using var db = await Database();
        SeedCall(db);
        Execute(db, "UPDATE phonecalls SET localoriginalvalue=''"); // Unknown may carry an empty original; Available may not.
        foreach (var (column, value) in new (string, object?)[]
        {
            ("state", 2), ("state", 4), ("state", 8), ("direction", 0), ("revision", -1),
            ("remoteoriginalvalue", null), ("remotepresentation", 2), ("localpresentation", 1),
            ("remotee164", "+01"), ("endreason", 2), ("endedatutc", Time),
            ("answeredatutc", Time), ("statechangedatutc", Time - 1)
        }) RejectUpdate(db, "phonecalls", column, value);
        Execute(db, "UPDATE phonecalls SET state=4,answeredatutc=startedatutc");
        Execute(db, "UPDATE phonecalls SET state=5");
        RejectUpdate(db, "phonecalls", "answeredatutc", null);
        Execute(db, "UPDATE phonecalls SET state=6,endedatutc=statechangedatutc,endreason=1");
        RejectUpdate(db, "phonecalls", "endreason", 2);
        RejectUpdate(db, "phonecalls", "endreason", 7);
        RejectUpdate(db, "phonecalls", "endedatutc", Time + 1);
        Execute(db, "UPDATE phonecalls SET state=7,endreason=9");
        RejectUpdate(db, "phonecalls", "endreason", 1);
    }

    [TestMethod]
    public async Task CallOriginsAreMandatoryAndQueriesCanSeparatePcsAndWindowsUsers()
    {
        using var db = await Database();
        SeedCall(db);
        Assert.AreEqual("PC-Ä-01", Scalar(db, "SELECT machinename FROM phonecalls"));
        Assert.AreEqual("DOMAIN\\Benutzer", Scalar(db, "SELECT windowsusername FROM phonecalls"));
        foreach (var (column, value) in new (string, object?)[]
        {
            ("workstationid", null), ("workstationid", new string('0', 32)),
            ("machinename", null), ("machinename", ""), ("machinename", new string('x', 257)),
            ("windowsusername", null), ("windowsusername", "\t\u3000"), ("windowsusername", new string('x', 257))
        }) RejectUpdate(db, "phonecalls", column, value);
        Execute(db, "INSERT INTO phonecalls SELECT @value,companyid,writeversion,revision,'" + Id('b') + "','PC-02','DOMAIN\\Andere',direction,state,localpresentation,localoriginalvalue,locale164,localextension,remotepresentation,remoteoriginalvalue,remotee164,remoteextension,startedatutc,statechangedatutc,answeredatutc,endedatutc,endreason FROM phonecalls", Id('3'));
        Assert.AreEqual(1L, Scalar(db, "SELECT COUNT(*) FROM phonecalls WHERE machinename='PC-02' AND windowsusername='DOMAIN\\Andere'"));
        Assert.AreEqual(1L, Scalar(db, "SELECT COUNT(*) FROM phonecalls WHERE machinename='PC-Ä-01'"));
        Assert.AreEqual(1L, Scalar(db, "SELECT COUNT(*) FROM phonecalls WHERE workstationid='" + Id('b') + "'"));
        Assert.AreEqual(3L, Scalar(db, "SELECT COUNT(*) FROM sqlite_schema WHERE type='index' AND name IN ('ix_phonecalls_workstation','ix_phonecalls_machine','ix_phonecalls_user')"));
    }

    [TestMethod]
    public async Task FailedProductUpgradeRetainsDataAndConfirmedVersionsThenResumesAfterRepair()
    {
        var target = Target();
        await new DatabaseProvisioner(new()).CreateAsync(target);
        var engine = new FluentMigratorEngine(DatabaseType.Sqlite);
        engine.MigrateUp(target, 2);
        using (var db = Open(target))
        {
            Insert(db, "companies", Company());
            Execute(db, "CREATE TABLE recordings (fixture INTEGER)");
        }
        var migrator = new DatabaseMigrator(new(), engine);
        var failure = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => migrator.MigrateAsync(target));
        Assert.AreEqual(DatabaseErrorCode.MigrationFailed, failure.Code);
        var state = await new DatabaseStatusService(new(), engine.Catalog).GetStatusAsync(target);
        CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, state.AppliedVersions.ToArray());
        using (var db = Open(target))
        {
            Assert.AreEqual("Mandant Ä 电话 ' 001", Scalar(db, "SELECT displayname FROM companies"));
            Assert.AreEqual(0L, Scalar(db, "SELECT COUNT(*) FROM sqlite_schema WHERE name='numberparselogs'"));
            Execute(db, "DROP TABLE recordings"); // Remove only the deliberate test collision.
        }
        Assert.AreEqual(DatabaseSchemaState.Current, (await migrator.MigrateAsync(target)).State);
    }

    [TestMethod]
    public async Task NotesEventsAndRecordingsKeepIndependentVersionsAndRejectInvalidValues()
    {
        using var db = await Database();
        SeedCall(db);
        Insert(db, "callnotes", new() { ["id"] = Id('3'), ["callid"] = Id('2'), ["text"] = "Notiz 电话",
            ["writeversion"] = 1L, ["createdatutc"] = Time, ["updatedatutc"] = Time + 1 });
        var item = new Dictionary<string, object?> { ["id"] = Id('4'), ["callid"] = Id('2'), ["sequence"] = 1L,
            ["occurredatutc"] = Time + 1, ["state"] = 3, ["code"] = "call.ringing-1" };
        Insert(db, "callevents", item);
        item["id"] = Id('5');
        RejectInsert(db, "callevents", item);
        RejectUpdate(db, "callevents", "code", "Benutzer 电话");
        RejectUpdate(db, "callevents", "sequence", 0);
        RejectUpdate(db, "callnotes", "text", "\u2000\t");
        RejectUpdate(db, "callnotes", "text", new string('x', 32769));
        RejectUpdate(db, "callnotes", "updatedatutc", Time - 1);
        Insert(db, "recordings", new() { ["id"] = Id('6'), ["callid"] = Id('2'), ["relativepath"] = "2026/电话.wav",
            ["format"] = "wav-pcm16", ["durationticks"] = 1L, ["availability"] = 1, ["writeversion"] = 1L,
            ["createdatutc"] = Time, ["updatedatutc"] = Time + 1 });
        Assert.AreEqual(1L, Scalar(db, "SELECT durationticks FROM recordings"));
        foreach (var path in new[] { "../a.wav", "/a.wav", "C:/a.wav", "a\\b.wav", "a/../b.wav", "a//b.wav", "a/.", "a/" })
            RejectUpdate(db, "recordings", "relativepath", path);
        RejectUpdate(db, "recordings", "availability", 4);
        RejectUpdate(db, "recordings", "durationticks", -1);
        Assert.AreEqual(1L, Scalar(db, "SELECT writeversion FROM phonecalls"));
        Assert.Throws<DbException>(() => Execute(db, "DELETE FROM phonecalls"));
    }

    [TestMethod]
    public async Task NumberErrorsRetainEmptyOriginalsAndAreIndependentOfCachedParties()
    {
        using var db = await Database();
        Insert(db, "companies", Company());
        var row = new Dictionary<string, object?> { ["id"] = Id('2'), ["companyid"] = Id('1'), ["partykeyhash"] = Hash('c'),
            ["kind"] = 1, ["externalid"] = "00001", ["phonekind"] = 1, ["sourcerecordtype"] = "Customer",
            ["sourcerecordid"] = "00001", ["sourcefieldname"] = "Phone", ["originalvalue"] = "", ["code"] = "number.empty", ["occurredatutc"] = Time };
        Insert(db, "numberparselogs", row);
        Assert.AreEqual("", Scalar(db, "SELECT originalvalue FROM numberparselogs"));
        RejectUpdate(db, "numberparselogs", "code", "nummer ungültig");
        RejectUpdate(db, "numberparselogs", "phonekind", 4);
        RejectUpdate(db, "numberparselogs", "ownerexternalid", "unexpected");
        Assert.Throws<DbException>(() => Execute(db, "DELETE FROM companies"));
    }

    [TestMethod]
    [DataRow(DatabaseType.Sqlite)]
    [DataRow(DatabaseType.SqlServer)]
    [DataRow(DatabaseType.MySql)]
    [DataRow(DatabaseType.PostgreSql)]
    public void EveryProviderHasFiveVersionsAndExplicitDdlForItsPhysicalTypesAndSelectionIndex(DatabaseType type)
    {
        var engine = new FluentMigratorEngine(type);
        CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4, 5 }, engine.Catalog.Versions.Take(5).ToArray());
        var ddl = string.Join(";\n", Enumerable.Range(1, 5).SelectMany(version => InitialSchema.Statements(type, version)));
        Assert.AreEqual(11, ddl.Split("CREATE TABLE", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("DROP ", ddl);
        Assert.DoesNotContain("CASCADE", ddl);
        Assert.Contains("ux_partyaddresses_owner", ddl);
        Assert.Contains("ux_callparties_selected", ddl);
        Assert.Contains("3155378975999999999", ddl);
        Assert.Contains("CHECK", ddl);
        var constraintNames = System.Text.RegularExpressions.Regex.Matches(ddl, """CONSTRAINT [\[`"]([a-z0-9_]+)[\]`"]""")
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.AreEqual(constraintNames.Length, constraintNames.Distinct(StringComparer.Ordinal).Count());
        if (type == DatabaseType.MySql)
        {
            Assert.Contains("ENGINE=InnoDB", ddl);
            Assert.Contains("CHARACTER SET ascii COLLATE ascii_bin", ddl);
            Assert.Contains("LONGTEXT CHARACTER SET utf8mb4", ddl);
            Assert.Contains("GENERATED ALWAYS AS", ddl);
            Assert.Contains("ELSE NULL END) STORED", ddl);
        }
        else
        {
            Assert.DoesNotContain("GENERATED ALWAYS AS", ddl);
            Assert.Contains("CREATE UNIQUE INDEX", ddl);
            Assert.Contains("WHERE", ddl);
        }
        if (type == DatabaseType.SqlServer) { Assert.Contains("NVARCHAR(MAX)", ddl); Assert.Contains("Latin1_General_100_BIN2", ddl); }
        if (type == DatabaseType.PostgreSql) Assert.Contains("COLLATE \"C\"", ddl);
        if (type == DatabaseType.Sqlite) Assert.Contains("typeof(", ddl);
    }

    private DatabaseTarget Target() => DatabaseTarget.Sqlite(Path.Combine(_root, "schema.db"));
    private async Task<DbConnection> Database()
    {
        var target = Target();
        await new DatabaseProvisioner(new()).CreateAsync(target);
        await new DatabaseMigrator(new(), new FluentMigratorEngine(DatabaseType.Sqlite)).MigrateAsync(target);
        return Open(target);
    }

    private static DbConnection Open(DatabaseTarget target)
    {
        var connection = new DatabaseFactory().CreateConnection(target);
        connection.Open();
        return connection;
    }

    internal static void Execute(DbConnection db, string sql, object? value = null)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        var parameter = command.CreateParameter(); parameter.ParameterName = "@value"; parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
        command.ExecuteNonQuery();
    }

    internal static object? Scalar(DbConnection db, string sql)
    {
        using var command = db.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar();
    }

    internal static void Insert(DbConnection db, string table, Dictionary<string, object?> row)
    {
        using var command = db.CreateCommand();
        command.CommandText = $"INSERT INTO \"{table}\" ({string.Join(",", row.Keys.Select(column => "\"" + column + "\""))}) VALUES ({string.Join(",", row.Keys.Select((_, index) => "@p" + index))})";
        foreach (var (value, index) in row.Values.Select((value, index) => (value, index)))
        {
            var parameter = command.CreateParameter(); parameter.ParameterName = "@p" + index; parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        command.ExecuteNonQuery();
    }

    private static void RejectInsert(DbConnection db, string table, Dictionary<string, object?> row) =>
        Assert.Throws<DbException>(() => Insert(db, table, row), "Expected constraint rejection in " + table);
    private static void RejectUpdate(DbConnection db, string table, string column, object? value) =>
        Assert.Throws<DbException>(() => Execute(db, $"UPDATE \"{table}\" SET \"{column}\"=@value", value), "Expected constraint rejection: " + table + "." + column);

    internal static Dictionary<string, object?> Company(char id = '1', char hash = 'a') => new()
    {
        ["id"] = Id(id), ["sourcehash"] = Hash(hash), ["productline"] = 1, ["installationid"] = "Installation-Ä",
        ["externalid"] = "0001", ["displayname"] = "Mandant Ä 电话 ' 001", ["writeversion"] = 1L,
        ["cacheversion"] = 0L, ["createdatutc"] = Time, ["updatedatutc"] = Time + 1
    };

    private static Dictionary<string, object?> Party(char id, char company, char hash) => new()
    {
        ["id"] = Id(id), ["companyid"] = Id(company), ["sourcehash"] = Hash(hash), ["kind"] = 1,
        ["externalid"] = "0001", ["displayname"] = "Kunde 电话", ["isinactive"] = 0, ["refreshedatutc"] = Time, ["searchname"] = "KUNDE 电话"
    };

    private static Dictionary<string, object?> Address() => new()
    {
        ["id"] = Id('5'), ["partyid"] = Id('3'), ["sourcehash"] = Hash('e'), ["externalid"] = "0001", ["kind"] = 1, ["position"] = 0,
        ["street"] = "Straße 电话", ["postalcode"] = "00123"
    };

    private static Dictionary<string, object?> Phone(char id, char party, char? address = null, int position = 0) => new()
    {
        ["id"] = Id(id), ["partyid"] = Id(party), ["addressid"] = address.HasValue ? Id(address.Value) : null,
        ["position"] = position, ["kind"] = 1, ["originalvalue"] = "+49 30 123456", ["e164"] = "+4930123456",
        ["sourcerecordtype"] = "Customer", ["sourcerecordid"] = "0001", ["sourcefieldname"] = "Phone"
    };

    private static void SeedCall(DbConnection db)
    {
        Insert(db, "companies", Company());
        Insert(db, "phonecalls", new() { ["id"] = Id('2'), ["companyid"] = Id('1'), ["writeversion"] = 1L,
            ["workstationid"] = Id('a'), ["machinename"] = "PC-Ä-01", ["windowsusername"] = "DOMAIN\\Benutzer",
            ["revision"] = 0L, ["direction"] = 1, ["state"] = 3, ["localpresentation"] = 0, ["remotepresentation"] = 1,
            ["remoteoriginalvalue"] = "+49 30 123456", ["startedatutc"] = Time, ["statechangedatutc"] = Time });
    }

    private static Dictionary<string, object?> Candidate(char id, char hash, int position, int selected) => new()
    {
        ["id"] = Id(id), ["callid"] = Id('2'), ["partykeyhash"] = Hash(hash), ["kind"] = 1, ["externalid"] = "0001",
        ["companyid"] = Id('1'), ["assignedcompanyid"] = selected == 1 ? Id('1') : null, ["matchkind"] = 0,
        ["displayname"] = "Historisch Ä 电话", ["isselected"] = selected, ["position"] = position
    };
}
