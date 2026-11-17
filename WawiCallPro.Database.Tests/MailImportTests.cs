using Dapper;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Office;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class MailImportTests
{
    [TestMethod]
    public async Task MailPagesAndCheckpointsAreAtomicAndScopeIsExplicit()
    {
        var root = Path.Combine(Path.GetTempPath(), "wcp-mail-" + Guid.NewGuid().ToString("N"));
        try
        {
            var target = DatabaseTarget.Sqlite(Path.Combine(root, "mail.db"));
            await new DatabaseProvisioner(new()).CreateAsync(target);
            await new DatabaseMigrator(new(), new FluentMigratorEngine(DatabaseType.Sqlite)).MigrateAsync(target);
            await MailImportAcceptance.RunAsync(target);
            await using var db = new DatabaseFactory().CreateConnection(target); await db.OpenAsync();
            var rootDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            while (rootDirectory is not null && !File.Exists(Path.Combine(rootDirectory.FullName, "Docs", "Datenbank-Tabellen-und-Spalten.md"))) rootDirectory = rootDirectory.Parent;
            Assert.IsNotNull(rootDirectory);
            var documentation = await File.ReadAllLinesAsync(Path.Combine(rootDirectory.FullName, "Docs", "Datenbank-Tabellen-und-Spalten.md"));
            foreach (var table in new[] { "mailaccounts", "mailmessages", "mailaddresses", "mailmemberships", "mailsyncstate" })
            {
                var columns = (await db.QueryAsync<string>("SELECT name FROM pragma_table_info(@table)", new { table })).ToArray();
                var described = documentation.SkipWhile(line => !line.StartsWith("## " + table + " ", StringComparison.Ordinal)).Skip(1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal))
                    .Select(line => System.Text.RegularExpressions.Regex.Match(line, @"^\|\s*`([^`]+)`\s*\|")).Where(match => match.Success).Select(match => match.Groups[1].Value).ToArray();
                CollectionAssert.AreEquivalent(columns, described); Assert.AreEqual(columns.Length, described.Distinct().Count());
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

/// <summary>Shared acceptance runs unchanged against SQLite and all four isolated server engines.</summary>
public static class MailImportAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        var repository = new DapperMailImportRepository(new(target, new()));
        var account = new MailImportAccount(Guid.NewGuid(), MailProviderKind.Microsoft365, MailIdentity.Hash("binding"));
        var inbox = MailIdentity.Hash("inbox"); var archive = MailIdentity.Hash("archive");
        var address = MailIdentity.Hash("customer@example.invalid"); var other = MailIdentity.Hash("someone@example.invalid");
        var generation = Guid.NewGuid(); var time = RepositoryAcceptance.Time;
        MailImportRecord Message(string stable, string source, string revision, params string[] folders) =>
            new(MailIdentity.Hash(stable), MailIdentity.Hash(source), MailIdentity.Hash(revision), time, "protected synthetic envelope " + revision, false, [address], [.. folders]);
        var first = new MailImportWrite(account, inbox, null, generation, true, null, "protected page2", MailIdentity.Hash("page1"), false,
            [Message("stable1", "entry1", "rev1", inbox), Message("stable2", "entry2", "rev2", inbox)], [], time);
        var state = await repository.ApplyAsync(first);
        Assert.AreEqual(1L, state.Version); Assert.AreEqual("protected page2", (await repository.GetStateAsync(account, inbox))!.NextPage);
        Assert.AreEqual(state, await repository.ApplyAsync(first));
        Assert.AreEqual(2, (await repository.GetPageAsync(new(account.Id, [inbox]))).Items.Length);
        Assert.AreEqual(0, (await repository.GetPageAsync(new(account.Id, []))).Items.Length);
        Assert.AreEqual(0, (await repository.GetPageAsync(new(account.Id, [inbox], AddressHashes: []))).Items.Length);
        Assert.AreEqual(0, (await repository.GetPageAsync(new(account.Id, [inbox], AddressHashes: [other]))).Items.Length);
        Assert.AreEqual(2, (await repository.GetPageAsync(new(account.Id, [inbox], AddressHashes: [address]))).Items.Length);
        Assert.AreEqual(0, (await repository.GetPageAsync(new(Guid.NewGuid(), [inbox]))).Items.Length);
        await Assert.ThrowsAsync<RepositoryConflictException>(() => repository.GetStateAsync(account with { Binding = MailIdentity.Hash("different") }, inbox));
        var page = await repository.GetPageAsync(new(account.Id, [inbox], Limit: 1)); Assert.IsTrue(page.HasMore);
        var next = await repository.GetPageAsync(new(account.Id, [inbox], AfterId: page.Items[0].Id, Limit: 1)); Assert.AreEqual(1, next.Items.Length); Assert.AreNotEqual(page.Items[0].Id, next.Items[0].Id);

        // A stale state rolls back even a valid message update.
        var stale = first with { ExpectedVersion = 99, Messages = [Message("stable1", "changed", "changed", inbox)], PageHash = MailIdentity.Hash("stale") };
        await Assert.ThrowsAsync<RepositoryConflictException>(() => repository.ApplyAsync(stale));
        Assert.IsNotNull(await repository.GetAsync(account.Id, MailIdentity.Hash("entry1")));
        var final = first with { ExpectedVersion = 1, NextPage = null, Checkpoint = "protected checkpoint", Messages = [], PageHash = MailIdentity.Hash("page2"), CompleteSnapshot = true, ChangedAtUtc = time.AddTicks(1) };
        state = await repository.ApplyAsync(final);
        Assert.AreEqual(2, (await repository.GetPageAsync(new(account.Id, [inbox]))).Items.Length);

        // A fresh, incomplete scan cannot delete the second, unseen message.
        var restarted = final with { ExpectedVersion = 2, Generation = Guid.NewGuid(), NextPage = "pending", CompleteSnapshot = false, Messages = [Message("stable1", "entry1", "rev1", inbox)], PageHash = MailIdentity.Hash("restart"), ChangedAtUtc = time.AddTicks(2) };
        await repository.ApplyAsync(restarted);
        Assert.AreEqual(2, (await repository.GetPageAsync(new(account.Id, [inbox]))).Items.Length);
        await repository.ApplyAsync(restarted with { ExpectedVersion = 3, NextPage = null, CompleteSnapshot = true, Messages = [], PageHash = MailIdentity.Hash("restart done"), ChangedAtUtc = time.AddTicks(3) });
        Assert.AreEqual(1, (await repository.GetPageAsync(new(account.Id, [inbox]))).Items.Length);
        Assert.IsTrue((await repository.GetStableAsync(account.Id, MailIdentity.Hash("stable2")))!.Hidden);

        // A move changes EntryID but preserves the stable imported object.
        var before = await repository.GetStableAsync(account.Id, MailIdentity.Hash("stable1"));
        var move = new MailImportWrite(account, archive, null, Guid.NewGuid(), false, "protected delta", null, MailIdentity.Hash("move"), false,
            [Message("stable1", "movedEntry", "rev3", archive)], [], time.AddTicks(4));
        await repository.ApplyAsync(move);
        var moved = await repository.GetAsync(account.Id, MailIdentity.Hash("movedEntry"));
        Assert.AreEqual(before!.Id, moved!.Id); Assert.AreEqual(before.CreatedAtUtc, moved.CreatedAtUtc);
        Assert.AreEqual(0, (await repository.GetPageAsync(new(account.Id, [inbox]))).Items.Length);
        Assert.AreEqual(1, (await repository.GetPageAsync(new(account.Id, [archive]))).Items.Length);
        await repository.ApplyAsync(move with { ExpectedVersion = 1, Messages = [], RemovedSourceHashes = [MailIdentity.Hash("movedEntry")], PageHash = MailIdentity.Hash("delete"), ChangedAtUtc = time.AddTicks(5) });
        Assert.IsNull(await repository.GetAsync(account.Id, MailIdentity.Hash("movedEntry")));
        Assert.IsTrue((await repository.GetStableAsync(account.Id, MailIdentity.Hash("stable1")))!.Hidden);

        // Force a database constraint failure after one message: neither that message nor its cursor may commit.
        var broken = first with { Account = account with { Id = Guid.NewGuid() }, Generation = Guid.NewGuid(), Messages = [Message("rollback1", "r1", "r1", inbox), Message("rollback2", "r1", "r2", inbox)] };
        await Assert.ThrowsAsync<DatabaseOperationException>(() => repository.ApplyAsync(broken));
        Assert.IsNull(await repository.GetStateAsync(broken.Account, inbox));
        Assert.IsNull(await repository.GetStableAsync(broken.Account.Id, MailIdentity.Hash("rollback1")));
    }
}
