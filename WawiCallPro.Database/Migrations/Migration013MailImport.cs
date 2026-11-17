using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Account-bound encrypted mail snapshots with atomic page/checkpoint acknowledgement.</summary>
[Migration(13, "Read-only mail import and synchronization checkpoints")]
public sealed class Migration013MailImport(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);
        var accounts = Create.Table("mailaccounts").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_mailaccounts")
            .WithColumn("provider").AsInt32().NotNullable()
            .WithColumn("binding").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("createdatutc").AsInt64().NotNullable();
        accounts.WithCheckConstraint(sql, "ck_mailaccounts_id", sql.Hex(sql.Quote("id"), 32) + " AND " + sql.Quote("id") + " <> '00000000000000000000000000000000'");
        accounts.WithCheckConstraint(sql, "ck_mailaccounts_provider", sql.IntegerCheck("provider", 1, 3));
        accounts.WithCheckConstraint(sql, "ck_mailaccounts_binding", sql.Hex(sql.Quote("binding"), 64));
        accounts.WithCheckConstraint(sql, "ck_mailaccounts_created", sql.IntegerCheck("createdatutc", 0, 3155378975999999999));

        var messages = Create.Table("mailmessages").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(64)).NotNullable().PrimaryKey("pk_mailmessages")
            .WithColumn("accountid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("stablehash").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("sourcehash").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("revisionhash").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("receivedatutc").AsInt64().NotNullable()
            .WithColumn("payload").AsCustom(sql.UnicodeType(4194304)).NotNullable()
            .WithColumn("hidden").AsInt32().NotNullable()
            .WithColumn("createdatutc").AsInt64().NotNullable()
            .WithColumn("updatedatutc").AsInt64().NotNullable();
        messages.WithForeignKeyConstraint(sql, "fk_mailmessages_account", ["accountid"], "mailaccounts", "id");
        messages.WithCheckConstraint(sql, "ck_mailmessages_id", sql.Hex(sql.Quote("id"), 64));
        messages.WithCheckConstraint(sql, "ck_mailmessages_stable", sql.Hex(sql.Quote("stablehash"), 64));
        messages.WithCheckConstraint(sql, "ck_mailmessages_source", sql.Hex(sql.Quote("sourcehash"), 64));
        messages.WithCheckConstraint(sql, "ck_mailmessages_revision", sql.Hex(sql.Quote("revisionhash"), 64));
        messages.WithCheckConstraint(sql, "ck_mailmessages_payload", sql.TextCheck("payload", 4194304, nonBlank: true));
        messages.WithCheckConstraint(sql, "ck_mailmessages_hidden", sql.IntegerCheck("hidden", 0, 1));
        messages.WithCheckConstraint(sql, "ck_mailmessages_received", sql.IntegerCheck("receivedatutc", 0, 3155378975999999999));
        messages.WithCheckConstraint(sql, "ck_mailmessages_created", sql.IntegerCheck("createdatutc", 0, 3155378975999999999));
        messages.WithCheckConstraint(sql, "ck_mailmessages_updated", sql.IntegerCheck("updatedatutc", 0, 3155378975999999999) + " AND [updatedatutc] >= [createdatutc]");
        Create.Index("ux_mailmessages_stable").OnTable("mailmessages").InSchema(sql.SchemaName).OnColumn("accountid").Ascending().OnColumn("stablehash").Ascending().WithOptions().Unique();
        Create.Index("ux_mailmessages_source").OnTable("mailmessages").InSchema(sql.SchemaName).OnColumn("accountid").Ascending().OnColumn("sourcehash").Ascending().WithOptions().Unique();

        var addresses = Create.Table("mailaddresses").InSchema(sql.SchemaName)
            .WithColumn("messageid").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("addresshash").AsCustom(sql.HexType(64)).NotNullable();
        addresses.WithForeignKeyConstraint(sql, "fk_mailaddresses_message", ["messageid"], "mailmessages", "id");
        addresses.WithCheckConstraint(sql, "ck_mailaddresses_hash", sql.Hex(sql.Quote("addresshash"), 64));
        Create.Index("ux_mailaddresses_pair").OnTable("mailaddresses").InSchema(sql.SchemaName).OnColumn("messageid").Ascending().OnColumn("addresshash").Ascending().WithOptions().Unique();
        Create.Index("ix_mailaddresses_hash").OnTable("mailaddresses").InSchema(sql.SchemaName).OnColumn("addresshash").Ascending();

        var memberships = Create.Table("mailmemberships").InSchema(sql.SchemaName)
            .WithColumn("messageid").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("folderhash").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("generation").AsCustom(sql.HexType(32)).NotNullable();
        memberships.WithForeignKeyConstraint(sql, "fk_mailmemberships_message", ["messageid"], "mailmessages", "id");
        memberships.WithCheckConstraint(sql, "ck_mailmemberships_folder", sql.Hex(sql.Quote("folderhash"), 64));
        memberships.WithCheckConstraint(sql, "ck_mailmemberships_generation", sql.Hex(sql.Quote("generation"), 32));
        Create.Index("ux_mailmemberships_pair").OnTable("mailmemberships").InSchema(sql.SchemaName).OnColumn("messageid").Ascending().OnColumn("folderhash").Ascending().WithOptions().Unique();
        Create.Index("ix_mailmemberships_folder").OnTable("mailmemberships").InSchema(sql.SchemaName).OnColumn("folderhash").Ascending();

        var state = Create.Table("mailsyncstate").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(64)).NotNullable().PrimaryKey("pk_mailsyncstate")
            .WithColumn("accountid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("folderhash").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("checkpoint").AsCustom(sql.UnicodeType(262144)).Nullable()
            .WithColumn("nextpage").AsCustom(sql.UnicodeType(262144)).Nullable()
            .WithColumn("generation").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("snapshot").AsInt32().NotNullable()
            .WithColumn("lastpagehash").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("writeversion").AsInt64().NotNullable()
            .WithColumn("updatedatutc").AsInt64().NotNullable();
        state.WithForeignKeyConstraint(sql, "fk_mailsyncstate_account", ["accountid"], "mailaccounts", "id");
        state.WithCheckConstraint(sql, "ck_mailsyncstate_id", sql.Hex(sql.Quote("id"), 64));
        state.WithCheckConstraint(sql, "ck_mailsyncstate_folder", sql.Hex(sql.Quote("folderhash"), 64));
        state.WithCheckConstraint(sql, "ck_mailsyncstate_generation", sql.Hex(sql.Quote("generation"), 32));
        state.WithCheckConstraint(sql, "ck_mailsyncstate_checkpoint", sql.TextCheck("checkpoint", 262144, nullable: true));
        state.WithCheckConstraint(sql, "ck_mailsyncstate_next", sql.TextCheck("nextpage", 262144, nullable: true));
        state.WithCheckConstraint(sql, "ck_mailsyncstate_snapshot", sql.IntegerCheck("snapshot", 0, 1));
        state.WithCheckConstraint(sql, "ck_mailsyncstate_pagehash", sql.Hex(sql.Quote("lastpagehash"), 64));
        state.WithCheckConstraint(sql, "ck_mailsyncstate_version", sql.IntegerCheck("writeversion", 1, long.MaxValue));
        state.WithCheckConstraint(sql, "ck_mailsyncstate_updated", sql.IntegerCheck("updatedatutc", 0, 3155378975999999999));
        Create.Index("ux_mailsyncstate_folder").OnTable("mailsyncstate").InSchema(sql.SchemaName).OnColumn("accountid").Ascending().OnColumn("folderhash").Ascending().WithOptions().Unique();
    }
}
