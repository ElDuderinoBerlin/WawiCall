using FluentMigrator.Builders.Create.Table;
using FluentMigrator.Expressions;

namespace WawiCallPro.Database.Schema;

/// <summary>
/// FluentMigrator has no table CHECK builder. Keep inline constraints on its table expression so
/// SQLite can create them with the table, without rebuilding it or replacing checks with triggers.
/// </summary>
internal static class TableConstraintExtensions
{
    private const string Feature = "WawiCallPro.InlineConstraints";

    internal static ICreateTableColumnOptionOrWithColumnSyntax WithCheckConstraint(
        this ICreateTableColumnOptionOrWithColumnSyntax table, SchemaSql sql, string name, string predicate) =>
        table.WithTableConstraint($"CONSTRAINT {sql.Quote(name)} CHECK ({sql.Expression(predicate)})");

    internal static ICreateTableColumnOptionOrWithColumnSyntax WithForeignKeyConstraint(
        this ICreateTableColumnOptionOrWithColumnSyntax table, SchemaSql sql, string name,
        string[] columns, string parent, params string[] parentColumns) =>
        table.WithTableConstraint($"CONSTRAINT {sql.Quote(name)} FOREIGN KEY ({string.Join(", ", columns.Select(sql.Quote))}) " +
            $"REFERENCES {sql.Table(parent)} ({string.Join(", ", parentColumns.Select(sql.Quote))}) ON DELETE NO ACTION ON UPDATE NO ACTION");

    internal static ICreateTableColumnOptionOrWithColumnSyntax WithTableConstraint(
        this ICreateTableColumnOptionOrWithColumnSyntax table, string constraint)
    {
        var expression = ((CreateTableExpressionBuilder)table).Expression;
        var features = expression.Columns[0].AdditionalFeatures;
        if (!features.TryGetValue(Feature, out var value)) features[Feature] = value = new List<string>();
        ((List<string>)value).Add(constraint);
        return table;
    }

    internal static string Generate(CreateTableExpression expression, Func<string> generate, DatabaseType type)
    {
        if (expression.Columns.Count == 0 || !expression.Columns[0].AdditionalFeatures.Remove(Feature, out var constraints))
            return generate();
        string sql;
        try { sql = generate(); }
        finally { expression.Columns[0].AdditionalFeatures[Feature] = constraints; }
        return Apply(expression, sql, type);
    }

    private static string Apply(CreateTableExpression expression, string generatedSql, DatabaseType type)
    {
        if (expression.Columns.Count == 0 || !expression.Columns[0].AdditionalFeatures.TryGetValue(Feature, out var value))
            return generatedSql;
        var position = generatedSql.LastIndexOf(')');
        if (position < 0) throw new InvalidOperationException("The table generator did not produce a table body.");
        var sql = generatedSql.Insert(position, ", " + string.Join(", ", (List<string>)value));
        return type == DatabaseType.MySql ? sql.TrimEnd(';') + " DEFAULT CHARACTER SET=utf8mb4" : sql;
    }
}
