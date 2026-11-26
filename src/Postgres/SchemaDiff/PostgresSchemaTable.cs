using System.Globalization;

namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaTable
{
    private const string RelationSql = """
        SELECT c.oid,c.relkind::text,c.relpersistence::text,c.relispartition,c.relrowsecurity,c.reloptions
        FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
        WHERE n.nspname=current_schema() AND c.relname=@table
        """;
    private const string ResolutionSql = """
        SELECT n.nspname FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
        WHERE c.oid=pg_catalog.to_regclass(pg_catalog.quote_ident(@table))
        """;

    internal static async Task<SchemaObservedTable> ReadAsync(
        PostgresSchemaRead read, string table, CancellationToken ct)
    {
        var parameters = PostgresSchemaRead.Parameter("table", table);
        var resolved = await read.RowsAsync(ResolutionSql, parameters, 1, ct);
        if (resolved.Count > 0 && (string)resolved[0][0]! != read.Request.Target.Scope.Namespace)
            throw new SchemaDiffException(SchemaDiffError.Ownership);
        var rows = await read.RowsAsync(RelationSql, parameters, 1, ct);
        if (rows.Count == 0) return Missing(table);
        var row = rows[0];
        var relation = Convert.ToUInt32(row[0], CultureInfo.InvariantCulture);
        var columns = await PostgresSchemaColumns.ReadAsync(read, relation, ct);
        var facts = await PostgresSchemaInventory.ReadAsync(read, relation, table, ct);
        if ((string)row[1]! != "r" || (string)row[2]! != "p" || (bool)row[3]! || (bool)row[4]! || row[5] is not null)
            facts = facts with
            { Constraints = SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Unsupported("table-effects") };
        return new SchemaObservedTable(table, SchemaFact<bool>.Known(true), columns, facts);
    }

    private static SchemaObservedTable Missing(string table)
    {
        var empty = SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Known([]);
        return new SchemaObservedTable(table, SchemaFact<bool>.Known(false), [],
            new SchemaObservedTableFacts(empty, empty, empty, empty));
    }
}
