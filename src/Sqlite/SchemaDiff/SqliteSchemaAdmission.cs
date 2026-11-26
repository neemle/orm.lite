using Data = global::Neemle.OrmLite.Sqlite.SqliteSchemaDataLiterals;
namespace Neemle.OrmLite.Sqlite;

internal static class SqliteSchemaAdmission
{
    internal static async Task<IReadOnlyList<object?[]>> ValidateAsync(SqliteSchemaRead read, CancellationToken ct)
    {
        if (read.Request.Target.Scope.Namespace != "main" || read.Request.Target.Scope.OwnedTables.Any(t =>
            string.Equals(t, SqliteDataLiterals.MarkerTable, StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, "sqlite_schema", StringComparison.OrdinalIgnoreCase)))
            throw new SchemaDiffException(SchemaDiffError.Ownership);
        var databases = await read.RowsAsync(Data.Databases, [],
            read.Request.Target.Limits.MaxCatalogObjects, ct);
        if (databases.Any(d => (string)d[0]! is not ("main" or "temp"))
            || databases.Any(d => (string)d[0]! == "main" && string.IsNullOrEmpty((string?)d[1])))
            throw new SchemaDiffException(SchemaDiffError.UnsupportedProvider);
        var shadows = await read.RowsAsync(Data.TempShadows, [],
            read.Request.Target.Limits.MaxCatalogObjects, ct);
        if (shadows.Any(s => read.Request.Target.Scope.OwnedTables.Contains(
            (string)s[0]!, StringComparer.OrdinalIgnoreCase)
            || string.Equals((string)s[0]!, "schema_migrations", StringComparison.OrdinalIgnoreCase)))
            throw new SchemaDiffException(SchemaDiffError.Ownership);
        foreach (var _ in databases.Concat(shadows)) read.Object();
        return databases;
    }
}
