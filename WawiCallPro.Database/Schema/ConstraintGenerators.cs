using FluentMigrator.Expressions;
using FluentMigrator.Runner.Generators.SQLite;
using FluentMigrator.Runner.Generators.SqlServer;
using FluentMigrator.Runner.Generators.MySql;
using FluentMigrator.Runner.Generators.Postgres;
using System.Data;

namespace WawiCallPro.Database.Schema;

// Normal FluentMigrator table/column/type generation remains authoritative. Only the table
// constraints missing from its builder API are appended before the generated closing parenthesis.
internal sealed class ConstraintSQLiteGenerator : SQLiteGenerator
{
    public override string Generate(CreateTableExpression expression) =>
        TableConstraintExtensions.Generate(expression, () => base.Generate(expression), DatabaseType.Sqlite);
}

internal sealed class ConstraintSqlServerGenerator : SqlServer2016Generator
{
    public override string Generate(CreateTableExpression expression) =>
        TableConstraintExtensions.Generate(expression, () => base.Generate(expression), DatabaseType.SqlServer);
}

internal sealed class ConstraintMySqlGenerator : MySql8Generator
{
    public ConstraintMySqlGenerator() : base(new MySqlQuoter(), new UnicodeMySqlTypeMap()) { }

    public override string Generate(CreateTableExpression expression) =>
        TableConstraintExtensions.Generate(expression, () => base.Generate(expression), DatabaseType.MySql);
}

internal sealed class UnicodeMySqlTypeMap : MySql8TypeMap
{
    public UnicodeMySqlTypeMap()
    {
        // MySQL's national NVARCHAR defaults to utf8mb3. AsString must retain four-byte Unicode.
        SetTypeMap(DbType.String, "VARCHAR(255) CHARACTER SET utf8mb4");
        SetTypeMap(DbType.String, "VARCHAR($size) CHARACTER SET utf8mb4", 255);
        SetTypeMap(DbType.String, "VARCHAR($size) CHARACTER SET utf8mb4", 4000);
        SetTypeMap(DbType.String, "LONGTEXT CHARACTER SET utf8mb4", int.MaxValue);
    }
}

internal sealed class ConstraintPostgresGenerator : Postgres15_0Generator
{
    public ConstraintPostgresGenerator() : base(new PostgresQuoter(new FluentMigrator.Runner.Processors.Postgres.PostgresOptions())) { }

    public override string Generate(CreateTableExpression expression) =>
        TableConstraintExtensions.Generate(expression, () => base.Generate(expression), DatabaseType.PostgreSql);
}
