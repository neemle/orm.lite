using Data = global::Neemle.OrmLite.Postgres.PostgresSchemaDataLiterals;
namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaInventory
{
    private const string Sql = Data.Inventory;

    internal static async Task<SchemaObservedTableFacts> ReadAsync(PostgresSchemaRead read,
        uint relation, string table, CancellationToken ct)
    {
        var rows = await read.RowsAsync(Sql, PostgresSchemaRead.Parameter("relation", relation),
            read.Request.Target.Limits.MaxCatalogObjects, ct);
        foreach (var _ in rows) read.Object();
        var roles = await PostgresSchemaRoles.ReadAsync(read, relation, ct);
        rows = PostgresSchemaRoles.Normalize(rows, roles, table);
        return new SchemaObservedTableFacts(Select(rows, "index"), Select(rows, "constraint:"),
            Select(rows, "trigger"), roles.Any(r => (string)r[2]! == "toast-unsupported")
                ? SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Unsupported("toast-options")
                : Select(rows, "dependent-"));
    }

    private static SchemaFact<IReadOnlyList<SchemaObjectStamp>> Select(IReadOnlyList<object?[]> rows, string prefix) =>
        SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Known(rows.Where(r => ((string)r[0]!).StartsWith(prefix,
            StringComparison.Ordinal)).Select(r => new SchemaObjectStamp((string)r[0]!, (string)r[1]!,
                PostgresSchemaRead.Hash((string)r[2]!))).ToArray());
}
