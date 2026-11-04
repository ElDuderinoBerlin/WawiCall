using System.Data.Common;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class MultiCompanySchemaTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-multicompany-" + Guid.NewGuid().ToString("N"));
    private static string Id(char value) => new(value, 32);
    private static string Hash(char value) => new(value, 64);
    private const long Time = 638000000000000001;

    [TestCleanup]
    public void Cleanup()
    {
        var full = Path.GetFullPath(_root);
        Assert.IsTrue(full.StartsWith(Path.Combine(Path.GetTempPath(), "WawiCallPro-multicompany-"), StringComparison.OrdinalIgnoreCase));
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }

    [TestMethod]
    public async Task VersionSixBackfillsCandidateCompanyAndPreservesLegacySnapshots()
    {
        var (target, engine) = await LegacyAsync();
        engine.MigrateUp(target, 6);
        await using var db = await OpenAsync(target);
        Assert.AreEqual(Id('1'), Scalar(db, "SELECT companyid FROM callparties"));
        Assert.AreEqual(Id('1'), Scalar(db, "SELECT assignedcompanyid FROM callparties"));
        Assert.AreEqual("Historisch Ä 电话", Scalar(db, "SELECT displayname FROM callparties"));
        Assert.AreEqual("0042", Scalar(db, "SELECT externalid FROM callparties"));
        Assert.AreEqual(0L, Scalar(db, "SELECT matchkind FROM callparties"));
        Assert.AreEqual(DBNull.Value, Scalar(db, "SELECT companydisplayname FROM callparties"));
        Assert.AreEqual(DBNull.Value, Scalar(db, "SELECT evidencejson FROM callparties"));
        Assert.AreEqual(0L, Scalar(db, "SELECT COUNT(*) FROM pragma_foreign_key_check"));
        Assert.AreEqual("ok", Scalar(db, "PRAGMA integrity_check"));
    }

    [TestMethod]
    public async Task HistoricalAlternativesKeepTheirOwnCompanyWhenSelectionChanges()
    {
        var (target, engine) = await LegacyAsync();
        engine.MigrateUp(target, 6);
        await using var db = await OpenAsync(target);
        ProductSchemaTests.Insert(db, "companies", ProductSchemaTests.Company('3', 'b'));
        ProductSchemaTests.Insert(db, "callparties", Candidate('5', '3', 'd', 1));
        Exec(db, "UPDATE callparties SET isselected=0,assignedcompanyid=NULL");
        Assert.Throws<DbException>(() => Exec(db, $"UPDATE callparties SET isselected=1,assignedcompanyid='{Id('3')}' WHERE id='{Id('5')}'"));
        Exec(db, $"UPDATE phonecalls SET companyid='{Id('3')}'");
        Exec(db, $"UPDATE callparties SET isselected=1,assignedcompanyid='{Id('3')}' WHERE id='{Id('5')}'");
        Assert.AreEqual(Id('1'), Scalar(db, $"SELECT companyid FROM callparties WHERE id='{Id('4')}'"));
        Assert.AreEqual(2L, Scalar(db, "SELECT COUNT(*) FROM callparties"));
        var duplicate = Candidate('6', '3', 'e', 2); duplicate["isselected"] = 1; duplicate["assignedcompanyid"] = Id('3');
        Assert.Throws<DbException>(() => ProductSchemaTests.Insert(db, "callparties", duplicate));
        Exec(db, "UPDATE callparties SET isselected=0,assignedcompanyid=NULL");
        Exec(db, "UPDATE phonecalls SET companyid=NULL");
        Assert.AreEqual(2L, Scalar(db, "SELECT COUNT(*) FROM callparties"));
        Assert.Throws<DbException>(() => Exec(db, "DELETE FROM companies"));
    }

    [TestMethod]
    public async Task ImpossibleLegacyCandidateOwnershipFailsBeforeNewDdlAndCanResumeAfterRepair()
    {
        var (target, engine) = await LegacyAsync();
        await using (var db = await OpenAsync(target)) Exec(db, "UPDATE phonecalls SET companyid=NULL");
        var failure = Assert.ThrowsExactly<DatabaseOperationException>(() => engine.MigrateUp(target, 6));
        Assert.AreEqual(DatabaseErrorCode.InvalidSchema, failure.Code);
        await using (var db = await OpenAsync(target))
        {
            Assert.AreEqual(0L, Scalar(db, "SELECT COUNT(*) FROM sqlite_schema WHERE name='callparties_v6'"));
            Assert.AreEqual(5L, Scalar(db, "SELECT MAX(version) FROM versioninfo"));
            Exec(db, $"UPDATE phonecalls SET companyid='{Id('1')}'");
        }
        engine.MigrateUp(target, 6);
        Assert.AreEqual(DatabaseSchemaState.Current, (await new DatabaseStatusService(new(), engine.Catalog).GetStatusAsync(target)).State);
    }

    [TestMethod]
    public async Task CompanyRulesAreVersionedBoundedAndIndependentOfTheReplaceablePartyCache()
    {
        var (target, engine) = await LegacyAsync(); engine.MigrateUp(target, 6);
        await using var db = await OpenAsync(target);
        Exec(db, $"INSERT INTO phonematchsettings(companyid,enabled,startwidth,writeversion,rulerevision,changedatutc) VALUES('{Id('1')}',1,5,1,1,{Time})");
        Assert.Throws<DbException>(() => Exec(db, "UPDATE phonematchsettings SET startwidth=0"));
        Assert.Throws<DbException>(() => Exec(db, "UPDATE phonematchsettings SET rulerevision=0"));
        Exec(db, $"INSERT INTO phonenumberblockrules(id,companyid,partykeyhash,partykind,externalid,kind,enabled,countrycallingcode,destinationcode,stem,minsuffixdigits,maxsuffixdigits,evidence,writeversion,changedatutc) VALUES('{Id('7')}','{Id('1')}','{Hash('f')}',1,'0042',1,1,49,'30','12345',0,4,'Explicit block',1,{Time})");
        Assert.AreEqual(0L, Scalar(db, "SELECT COUNT(*) FROM parties"));
        Assert.AreEqual(1L, Scalar(db, "SELECT COUNT(*) FROM phonenumberblockrules"));
        foreach (var sql in new[] { "UPDATE phonenumberblockrules SET destinationcode='3x'", "UPDATE phonenumberblockrules SET maxsuffixdigits=15", "UPDATE phonenumberblockrules SET partykind=3", "UPDATE phonenumberblockrules SET kind=0" })
            Assert.Throws<DbException>(() => Exec(db, sql));
    }

    private async Task<(DatabaseTarget, FluentMigratorEngine)> LegacyAsync()
    {
        var target = DatabaseTarget.Sqlite(Path.Combine(_root, "calls.db"));
        await new DatabaseProvisioner(new()).CreateAsync(target);
        var engine = new FluentMigratorEngine(DatabaseType.Sqlite); engine.MigrateUp(target, 5);
        await using var db = await OpenAsync(target);
        ProductSchemaTests.Insert(db, "companies", ProductSchemaTests.Company());
        Exec(db, $"INSERT INTO phonecalls(id,companyid,workstationid,machinename,windowsusername,writeversion,revision,direction,state,localpresentation,remotepresentation,startedatutc,statechangedatutc) VALUES('{Id('2')}','{Id('1')}','{Id('a')}','TEST-PC','DOMAIN\\User',1,0,1,3,0,0,{Time},{Time})");
        var old = Candidate('4', '1', 'c', 0); old.Remove("companyid"); old.Remove("assignedcompanyid"); old.Remove("matchkind"); old["isselected"] = 1;
        ProductSchemaTests.Insert(db, "callparties", old);
        return (target, engine);
    }

    private static Dictionary<string, object?> Candidate(char id, char company, char hash, int position) => new()
    {
        ["id"] = Id(id), ["callid"] = Id('2'), ["companyid"] = Id(company), ["assignedcompanyid"] = null,
        ["partykeyhash"] = Hash(hash), ["kind"] = 1, ["externalid"] = "0042", ["displayname"] = "Historisch Ä 电话",
        ["number"] = "+493012345", ["isselected"] = 0, ["position"] = position, ["matchkind"] = 0
    };
    private static async Task<DbConnection> OpenAsync(DatabaseTarget target)
    {
        var db = new DatabaseFactory().CreateConnection(target); await db.OpenAsync(); return db;
    }
    private static void Exec(DbConnection db, string sql) { using var command = db.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }
    private static object? Scalar(DbConnection db, string sql) { using var command = db.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar(); }
}
