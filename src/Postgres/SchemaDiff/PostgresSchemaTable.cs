using Data = global::Neemle.OrmLite.Postgres.PostgresSchemaDataLiterals;
using System.Globalization;

namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaTable
{
    private const string RelationSql = Data.TableFacts;
    private const string ResolutionSql = Data.ResolveTable;

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
