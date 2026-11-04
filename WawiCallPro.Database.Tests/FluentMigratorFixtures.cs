using FluentMigrator;

namespace WawiCallPro.Database.Tests
{
    // These fixtures exercise the real runner, not the product schema reserved for the next plan item.
    public abstract class OwnerFixtureMigration : ForwardOnlyMigration
    {
        public override void Up()
        {
            Create.Table(CommunicationDatabaseIdentity.TableName)
                .WithColumn("applicationid").AsString(64).PrimaryKey()
                .WithColumn("formatversion").AsInt32().NotNullable()
                .WithColumn("createdatutc").AsInt64().NotNullable();
            Insert.IntoTable(CommunicationDatabaseIdentity.TableName).Row(new
            {
                applicationid = CommunicationDatabaseIdentity.ApplicationId,
                formatversion = CommunicationDatabaseIdentity.FormatVersion,
                createdatutc = 0L
            });
            Create.Table("fixture_data").WithColumn("id").AsInt32().PrimaryKey()
                .WithColumn("value").AsString(100).NotNullable();
            Insert.IntoTable("fixture_data").Row(new { id = 1, value = "Grüße 电话" });
        }
    }
}

namespace WawiCallPro.Database.Tests.RunnerFixtures
{
    [Migration(1)]
    public sealed class CreateOwner : OwnerFixtureMigration;

    [Migration(2), Tags("SQLite")]
    public sealed class AddSqliteValue : ForwardOnlyMigration
    {
        public override void Up() => Insert.IntoTable("fixture_data").Row(new { id = 2, value = "SQLite" });
    }

    [Migration(2), Tags("PostgreSQL")]
    public sealed class PostgresOnly : ForwardOnlyMigration
    {
        public override void Up() => throw new InvalidOperationException("Wrong provider fixture was executed.");
    }
}

namespace WawiCallPro.Database.Tests.FailingRunnerFixtures
{
    [Migration(1)]
    public sealed class CreateOwner : OwnerFixtureMigration;

    [Migration(2, "private-runner-sentinel")]
    public sealed class FailAfterDdl : ForwardOnlyMigration
    {
        public override void Up()
        {
            Create.Table("fixture_unfinished").WithColumn("id").AsInt32();
            Execute.Sql("SELECT 'private-runner-sentinel' FROM nonexistent_fixture_table");
        }
    }
}

namespace WawiCallPro.Database.Tests.RunnerFixtures.Nested
{
    [Migration(1)]
    public sealed class NeverScanNested : ForwardOnlyMigration
    {
        public NeverScanNested() => throw new InvalidOperationException("Nested fixture was constructed.");
        public override void Up() => throw new InvalidOperationException();
    }
}

namespace WawiCallPro.Database.Tests.UnrelatedRunnerFixtures
{
    [Migration(1)]
    public sealed class NeverScanForeign : ForwardOnlyMigration
    {
        public NeverScanForeign() => throw new InvalidOperationException("Foreign fixture was constructed.");
        public override void Up() => throw new InvalidOperationException();
    }
}

namespace WawiCallPro.Database.Tests.ConstructorRunnerFixtures
{
    [Migration(41)]
    public sealed class NoConstructionDuringDiscovery : ForwardOnlyMigration
    {
        public NoConstructionDuringDiscovery() => throw new InvalidOperationException("Discovery must not construct migrations.");
        public override void Up() => throw new InvalidOperationException();
    }
}

namespace WawiCallPro.Database.Tests.FirstFailureRunnerFixtures
{
    [Migration(1)]
    public sealed class FailingOwner : OwnerFixtureMigration
    {
        public override void Up()
        {
            base.Up();
            Execute.Sql("SELECT 'private-runner-sentinel' FROM nonexistent_fixture_table");
        }
    }
}
