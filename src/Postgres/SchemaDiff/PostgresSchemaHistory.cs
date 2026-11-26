namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaHistory
{
    internal static async Task<SchemaObservationHistory> ReadAsync(PostgresSchemaRead read, CancellationToken ct)
    {
        var rows = await read.RowsAsync("SELECT c.relkind::text,n.nspname,c.relrowsecurity FROM pg_catalog.pg_class c "
            + "JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace "
            + "WHERE c.oid=pg_catalog.to_regclass(pg_catalog.quote_ident('schema_migrations'))", [], 1, ct);
        if (rows.Count == 0) return new(SchemaFact<bool>.Known(false),
            SchemaFact<IReadOnlyList<string>>.Known([]));
        if ((string)rows[0][1]! != read.Request.Target.Scope.Namespace)
            throw new SchemaDiffException(SchemaDiffError.Ownership);
        if ((string)rows[0][0]! != "r") return new(SchemaFact<bool>.Unsupported("history-kind"),
            SchemaFact<IReadOnlyList<string>>.Unknown("history-kind"));
        if ((bool)rows[0][2]!) return new(SchemaFact<bool>.Known(true),
            SchemaFact<IReadOnlyList<string>>.Unknown("history-row-security"));
        var ns = read.Request.Target.Scope.Namespace.Replace("\"", "\"\"", StringComparison.Ordinal);
        var versions = await read.RowsAsync("SELECT version FROM \"" + ns + "\".\"schema_migrations\"", [],
            read.Request.Target.Limits.MaxCatalogObjects, ct);
        return new(SchemaFact<bool>.Known(true),
            SchemaFact<IReadOnlyList<string>>.Known(versions.Select(r => (string)r[0]!).ToArray()));
    }
}
