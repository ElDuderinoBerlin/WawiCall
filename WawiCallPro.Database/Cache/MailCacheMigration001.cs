using FluentMigrator;

namespace WawiCallPro.Database.Cache;

/// <summary>SQLite-only extension of the reusable mail schema, with its own migration version table.</summary>
[Migration(1, "Local mail cache ownership and encrypted attachment blobs")]
public sealed class MailCacheMigration001 : ForwardOnlyMigration
{
    public override void Up()
    {
        Create.Table("mailcacheinfo")
            .WithColumn("id").AsString(64).NotNullable().PrimaryKey()
            .WithColumn("retentiondays").AsInt32().NotNullable();
        Insert.IntoTable("mailcacheinfo").Row(new { id = "WawiCallPro.MailCache.v1", retentiondays = 30 });
        Create.Table("mailattachmentblobs")
            .WithColumn("messageid").AsString(64).NotNullable().ForeignKey("mailmessages", "id")
            .WithColumn("attachmenthash").AsString(64).NotNullable()
            .WithColumn("revisionhash").AsString(64).NotNullable()
            .WithColumn("payload").AsBinary(int.MaxValue).NotNullable();
        Create.Index("ux_mailattachmentblobs_key").OnTable("mailattachmentblobs")
            .OnColumn("messageid").Ascending().OnColumn("attachmenthash").Ascending().WithOptions().Unique();
    }
}
