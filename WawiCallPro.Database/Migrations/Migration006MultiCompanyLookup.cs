using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

[Migration(6, "Independent candidate companies lookup evidence and phone rules")]
public sealed class Migration006MultiCompanyLookup(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        // Reject impossible legacy candidate identities before MySQL's first nontransactional DDL statement.
        Execute.WithConnection((connection, transaction) =>
        {
            using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = $"SELECT COUNT(*) FROM {DatabaseSql.Table(databaseType, "callparties")} p " +
                $"LEFT JOIN {DatabaseSql.Table(databaseType, "phonecalls")} c ON c.id=p.callid " +
                $"LEFT JOIN {DatabaseSql.Table(databaseType, "companies")} co ON co.id=c.companyid WHERE c.companyid IS NULL OR co.id IS NULL";
            if (Convert.ToInt64(command.ExecuteScalar()) != 0) throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema);
        });
        foreach (var statement in MultiCompanySchema.Statements(databaseType)) Execute.Sql(statement);
    }
}
