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
    internal string? SchemaName => Type switch
    {
        DatabaseType.SqlServer => "dbo", DatabaseType.PostgreSql => "public", _ => null
    };

    internal string TextCheck(string column, int maximum, bool nullable = false, bool nonBlank = false)
    {
        var value = Quote(column);
        var check = $"{Length(value)} <= {maximum}";
        if (Type == DatabaseType.Sqlite) check += $" AND typeof({value}) = 'text' AND instr({value}, char(0)) = 0";
        if (nonBlank) check += " AND " + NonBlank(value);
        return nullable ? $"{value} IS NULL OR ({check})" : check;
    }

    internal string IntegerCheck(string column, long minimum, long maximum, bool nullable = false)
    {
        var value = Quote(column);
        var check = $"{value} BETWEEN {minimum} AND {maximum}";
        if (Type == DatabaseType.Sqlite) check += $" AND typeof({value}) = 'integer'";
        return nullable ? $"{value} IS NULL OR ({check})" : check;
    }
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
        // The REGEXP operator exists in both MySQL and MariaDB; REGEXP_LIKE is MySQL-specific.
        DatabaseType.MySql => $"CONVERT({value} USING utf8mb4) COLLATE utf8mb4_bin REGEXP '^[{characters}]+$'",
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
        if (Type == DatabaseType.MySql) return $"{value} COLLATE utf8mb4_bin REGEXP '^[+][1-9][0-9]{{1,14}}$'";
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