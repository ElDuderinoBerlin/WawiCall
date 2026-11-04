using FluentMigrator.Runner.VersionTableInfo;

namespace WawiCallPro.Database;

/// <summary>Runner metadata uses the same lowercase names on every provider; it never owns the default schema.</summary>
internal sealed class CommunicationVersionTable(DatabaseType type) : IVersionTableMetaData
{
    public bool OwnsSchema => false;
    public string SchemaName => type switch
    {
        DatabaseType.SqlServer => "dbo",
        DatabaseType.PostgreSql => "public",
        _ => string.Empty
    };
    public string TableName => "versioninfo";
    public string ColumnName => "version";
    public string DescriptionColumnName => "description";
    public string UniqueIndexName => "uc_versioninfo_version";
    public string AppliedOnColumnName => "appliedon";
    public bool CreateWithPrimaryKey => false;
}
