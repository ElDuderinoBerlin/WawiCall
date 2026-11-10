using WawiCallPro.ConsoleApp.Diagnostics;
using WawiCallPro.Database;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class DatabaseCommandTests
{
    [TestMethod]
    public void PhoneRuleCommandsRequireExplicitWidthAndOptimisticWriteVersion()
    {
        var read = ConsoleCommand.Parse(["--phone-rule", "fixture", "0001"]);
        Assert.IsTrue(read.UsesDatabase); Assert.IsTrue(read.UsesPhoneMatching);
        Assert.IsNull(read.PhoneRule!.Enabled); Assert.AreEqual("0001", read.PhoneRule.CompanyId);
        var create = ConsoleCommand.Parse(["--phone-rule", "fixture", "0001", "on", "4", "new"]);
        Assert.AreEqual(true, create.PhoneRule!.Enabled); Assert.AreEqual(4, create.PhoneRule.StartWidth); Assert.IsNull(create.PhoneRule.ExpectedVersion);
        var update = ConsoleCommand.Parse(["--phone-rule", "fixture", "0001", "off", "5", "3"]);
        Assert.AreEqual(false, update.PhoneRule!.Enabled); Assert.AreEqual(3L, update.PhoneRule.ExpectedVersion);
        Assert.IsTrue(ConsoleCommand.Parse(["--phone-match", "fixture", "0001", "+4930123456789"]).UsesPhoneMatching);
        foreach (var args in new[]
        {
            new[] { "--phone-rule", "fixture", "0001", "on", "4" },
            ["--phone-rule", "fixture", "0001", "on", "0", "new"], ["--phone-rule", "fixture", "0001", "on", "16", "new"],
            ["--phone-rule", "fixture", "0001", "on", "4", "0"], ["--phone-rule", "fixture", "0001", "on", "4", "invalid"]
        }) Assert.ThrowsExactly<DiagnosticException>(() => ConsoleCommand.Parse(args));
    }

    [TestMethod]
    public void CommandsRequireExplicitActionsAndAbsoluteBackupPaths()
    {
        Assert.AreEqual("--status", ConsoleCommand.Parse([]).Name);
        Assert.IsFalse(ConsoleCommand.Parse(["--workstation-init"]).UsesDatabase);
        Assert.IsTrue(ConsoleCommand.Parse(["--db-status"]).UsesDatabase);
        Assert.AreEqual("D:\\a b\\backup.db", ConsoleCommand.Parse(["--db-backup", "D:\\a b\\backup.db"]).File);
        foreach (var arguments in new[] { new[] { "--db-backup" }, ["--db-restore", "relative.db"], ["--db-init", "sentinel-secret"], ["--unknown-sentinel-secret"] })
        {
            var failure = Assert.ThrowsExactly<DiagnosticException>(() => ConsoleCommand.Parse(arguments));
            Assert.DoesNotContain("sentinel-secret", failure.ToString());
        }
    }

    [TestMethod]
    public void DatabaseTableContainsOnlyAllowedStatusAndHelpListsEveryCommand()
    {
        var table = DatabaseCommands.Status(new(DatabaseType.Sqlite, DatabaseSchemaState.MigrationRequired, [1, 2], [3, 4, 5, 6, 7], 7));
        Assert.IsTrue(table.Rows.Any(row => row.Value == "MigrationRequired"));
        Assert.IsTrue(table.Rows.Any(row => row.Name == "Ausstehend" && row.Value.Contains('7')));
        var help = DiagnosticTables.Help();
        foreach (var name in new[] { "--db-status", "--db-init", "--db-migrate", "--db-approve-empty", "--db-backup", "--db-restore", "--workstation-init" })
            Assert.IsTrue(help.Rows.Any(row => row.Name.StartsWith(name, StringComparison.Ordinal)));
    }
}
