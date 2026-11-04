using System.Text;
using System.Text.RegularExpressions;

namespace WawiCallPro.Database.Schema;

/// <summary>
/// Provider-specific DDL for the immutable initial migrations. Inputs are schema constants, never application data.
/// Inline CHECK constraints are necessary because SQLite cannot add them to an existing table.
/// </summary>
internal sealed class SchemaSql(DatabaseType type)
{
    internal DatabaseType Type { get; } = type;
    internal string Table(string name) => DatabaseSql.Table(Type, name);
    internal string Quote(string name) => Type switch
    {
        DatabaseType.SqlServer => "[" + name + "]",
        DatabaseType.MySql => "`" + name + "`",
        _ => "\"" + name + "\""
    };

    internal string Expression(string expression) => Regex.Replace(expression, @"\[([a-z][a-z0-9_]*)\]", match => Quote(match.Groups[1].Value));
    internal string Length(string value) => Type switch
    {
        DatabaseType.SqlServer => $"(DATALENGTH({value}) / 2)",
        DatabaseType.MySql => $"CHAR_LENGTH({value})",
        _ => $"length({value})"
    };

    internal string UnicodeType(int length) => Type switch
    {
        DatabaseType.SqlServer => length > 4000 ? "NVARCHAR(MAX)" : $"NVARCHAR({length})",
        DatabaseType.MySql => length > 4000 ? "LONGTEXT CHARACTER SET utf8mb4" : $"VARCHAR({length}) CHARACTER SET utf8mb4",
        DatabaseType.PostgreSql => length > 4000 ? "TEXT" : $"VARCHAR({length})",
        _ => "TEXT"
    };

    internal string HexType(int length) => Type switch
    {
        DatabaseType.SqlServer => $"VARCHAR({length}) COLLATE Latin1_General_100_BIN2",
        DatabaseType.MySql => $"CHAR({length}) CHARACTER SET ascii COLLATE ascii_bin",
        DatabaseType.PostgreSql => $"VARCHAR({length}) COLLATE \"C\"",
        _ => "TEXT COLLATE BINARY"
    };

    internal string Character(int code) => Type switch
    {
        DatabaseType.SqlServer => $"NCHAR({code})",
        DatabaseType.MySql => $"CONVERT(0x{Convert.ToHexString(Encoding.UTF8.GetBytes(char.ConvertFromUtf32(code)))} USING utf8mb4)",
        DatabaseType.PostgreSql => $"chr({code})",
        _ => $"char({code})"
    };

    internal string NonBlank(string value)
    {
        // Frozen Unicode White_Space set; future framework changes must not alter a shipped migration.
        int[] whitespace = [9, 10, 11, 12, 13, 32, 133, 160, 5760, 8192, 8193, 8194, 8195, 8196,
            8197, 8198, 8199, 8200, 8201, 8202, 8232, 8233, 8239, 8287, 12288];
        foreach (var code in whitespace) value = $"REPLACE({value}, {Character(code)}, '')";
        return $"{Length(value)} > 0";
    }

    internal string AllowedAscii(string value, string characters) => Type switch
    {
        DatabaseType.Sqlite => $"{value} NOT GLOB '*[^{characters}]*'",
        DatabaseType.SqlServer => $"{value} COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^{characters}]%'",
        DatabaseType.MySql => $"REGEXP_LIKE({value}, '^[{characters}]+$', 'c')",
        _ => $"{value} ~ '^[{characters}]+$'"
    };

    internal string Hex(string value, int length)
    {
        var size = Type == DatabaseType.SqlServer ? $"DATALENGTH({value})" : Length(value);
        var check = $"{size} = {length} AND {AllowedAscii(value, "0-9a-f")}";
        return Type == DatabaseType.Sqlite ? check + $" AND typeof({value}) = 'text' AND instr({value}, char(0)) = 0" : check;
    }

    internal string Exact(string value, string literal)
    {
        var collation = Type switch
        {
            DatabaseType.SqlServer => " COLLATE Latin1_General_100_BIN2",
            DatabaseType.MySql => " COLLATE utf8mb4_bin",
            _ => string.Empty
        };
        return $"{value}{collation} = '{literal}' AND {Length(value)} = {literal.Length}";
    }

    internal string E164(string value)
    {
        if (Type == DatabaseType.MySql) return $"REGEXP_LIKE({value}, '^[+][1-9][0-9]{{1,14}}$', 'c')";
        if (Type == DatabaseType.PostgreSql) return $"{value} ~ '^[+][1-9][0-9]{{1,14}}$'";
        var rest = $"substr({value}, 2)";
        var first = $"substr({value}, 2, 1)";
        var plus = $"substr({value}, 1, 1)";
        if (Type == DatabaseType.SqlServer)
        {
            rest = $"SUBSTRING({value}, 2, 15)";
            first = $"SUBSTRING({value}, 2, 1)";
            plus = $"LEFT({value}, 1)";
        }
        return $"{Length(value)} BETWEEN 3 AND 16 AND {plus} = '+' AND {first} BETWEEN '1' AND '9' AND {AllowedAscii(rest, "0-9")}";
    }
}

internal sealed class SchemaTable(SchemaSql sql, string name)
{
    private readonly List<string> _columns = [];
    private readonly List<string> _constraints = [];
    private readonly List<string> _indexes = [];

    internal SchemaTable Id(string column = "id", bool nullable = false, bool primaryKey = false)
    {
        Column(column, sql.HexType(32), nullable);
        Check(column, Nullable(column, nullable, $"{sql.Hex(sql.Quote(column), 32)} AND {sql.Quote(column)} <> '00000000000000000000000000000000'"));
        if (primaryKey) PrimaryKey(column);
        return this;
    }

    internal SchemaTable Hash(string column)
    {
        Column(column, sql.HexType(64), false);
        return Check(column, sql.Hex(sql.Quote(column), 64));
    }

    internal SchemaTable Text(string column, int length, bool nullable = false, bool nonBlank = false)
    {
        Column(column, sql.UnicodeType(length), nullable);
        var value = sql.Quote(column);
        var check = $"{sql.Length(value)} <= {length}";
        if (sql.Type == DatabaseType.Sqlite) check += $" AND typeof({value}) = 'text' AND instr({value}, char(0)) = 0";
        if (nonBlank) check += " AND " + sql.NonBlank(value);
        return Check(column, Nullable(column, nullable, check));
    }

    internal SchemaTable Integer(string column, long minimum, long maximum, bool nullable = false, bool wide = false)
    {
        Column(column, sql.Type == DatabaseType.Sqlite ? "INTEGER" : wide ? "BIGINT" : "INT", nullable);
        var value = sql.Quote(column);
        var check = $"{value} BETWEEN {minimum} AND {maximum}";
        if (sql.Type == DatabaseType.Sqlite) check += $" AND typeof({value}) = 'integer'";
        return Check(column, Nullable(column, nullable, check));
    }

    internal SchemaTable Counter(string column, long minimum = 1, bool nullable = false) => Integer(column, minimum, long.MaxValue, nullable, wide: true);
    internal SchemaTable Utc(string column, bool nullable = false) => Integer(column, 0, 3155378975999999999, nullable, wide: true);
    internal SchemaTable Boolean(string column) => Integer(column, 0, 1);

    internal SchemaTable Check(string suffix, string expression)
    {
        _constraints.Add($"CONSTRAINT {sql.Quote("ck_" + name + "_" + suffix)} CHECK ({sql.Expression(expression)})");
        return this;
    }

    internal SchemaTable ForeignKey(string suffix, string[] columns, string parent, params string[] parentColumns)
    {
        _constraints.Add($"CONSTRAINT {sql.Quote("fk_" + name + "_" + suffix)} FOREIGN KEY ({Join(columns)}) REFERENCES {sql.Table(parent)} ({Join(parentColumns)}) ON DELETE NO ACTION ON UPDATE NO ACTION");
        return this;
    }

    internal SchemaTable Unique(string index, params string[] columns)
    {
        _indexes.Add($"CREATE UNIQUE INDEX {sql.Quote(index)} ON {sql.Table(name)} ({Join(columns)})");
        return this;
    }

    internal SchemaTable PrimaryKey(params string[] columns)
    {
        _constraints.Add($"CONSTRAINT {sql.Quote("pk_" + name)} PRIMARY KEY ({Join(columns)})");
        return this;
    }

    internal SchemaTable Index(string index, params string[] columns)
    {
        var ordered = columns.Select(column =>
        {
            var parts = column.Split(' ');
            return sql.Quote(parts[0]) + (parts.Length == 2 ? " " + parts[1] : string.Empty);
        });
        _indexes.Add($"CREATE INDEX {sql.Quote(index)} ON {sql.Table(name)} ({string.Join(", ", ordered)})");
        return this;
    }

    internal SchemaTable SelectedCallIndex()
    {
        if (sql.Type == DatabaseType.MySql)
        {
            _columns.Add($"{sql.Quote("selectedcallid")} {sql.HexType(32)} GENERATED ALWAYS AS (CASE WHEN {sql.Quote("isselected")} = 1 THEN {sql.Quote("callid")} ELSE NULL END) STORED");
            Unique("ux_callparties_selected", "selectedcallid");
        }
        else
            _indexes.Add($"CREATE UNIQUE INDEX {sql.Quote("ux_callparties_selected")} ON {sql.Table(name)} ({sql.Quote("callid")}) WHERE {sql.Quote("isselected")} = 1");
        return this;
    }

    internal IEnumerable<string> Statements()
    {
        var options = sql.Type == DatabaseType.MySql ? " ENGINE=InnoDB DEFAULT CHARACTER SET=utf8mb4" : string.Empty;
        yield return $"CREATE TABLE {sql.Table(name)} (\n{string.Join(",\n", _columns.Concat(_constraints))}\n){options}";
        foreach (var index in _indexes) yield return index;
    }

    private void Column(string column, string type, bool nullable) => _columns.Add($"{sql.Quote(column)} {type} {(nullable ? "NULL" : "NOT NULL")}");
    private string Nullable(string column, bool nullable, string check) => nullable ? $"{sql.Quote(column)} IS NULL OR ({check})" : check;
    private string Join(IEnumerable<string> columns) => string.Join(", ", columns.Select(sql.Quote));
}
