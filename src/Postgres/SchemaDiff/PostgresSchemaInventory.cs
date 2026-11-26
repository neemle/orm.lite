namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaInventory
{
    private const string Sql = """
        SELECT 'index',i.relname::text,pg_catalog.pg_get_indexdef(i.oid)
          FROM pg_catalog.pg_index x JOIN pg_catalog.pg_class i ON i.oid=x.indexrelid
          WHERE x.indrelid=CAST(@relation AS oid)
        UNION ALL SELECT 'constraint:'||c.contype::text,c.conname::text,pg_catalog.pg_get_constraintdef(c.oid,true)
          FROM pg_catalog.pg_constraint c WHERE c.conrelid=CAST(@relation AS oid)
        UNION ALL SELECT 'trigger',t.tgname::text,pg_catalog.pg_get_triggerdef(t.oid,true)
          FROM pg_catalog.pg_trigger t WHERE t.tgrelid=CAST(@relation AS oid)
        UNION ALL SELECT CASE WHEN
          (d.classid='pg_catalog.pg_type'::regclass AND EXISTS
            (SELECT 1 FROM pg_catalog.pg_type t WHERE t.oid=d.objid AND t.typrelid=CAST(@relation AS oid)))
          OR (d.classid='pg_catalog.pg_constraint'::regclass AND EXISTS
            (SELECT 1 FROM pg_catalog.pg_constraint c WHERE c.oid=d.objid AND c.conrelid=CAST(@relation AS oid)))
          OR (d.classid='pg_catalog.pg_class'::regclass AND EXISTS
            (SELECT 1 FROM pg_catalog.pg_index i WHERE i.indexrelid=d.objid AND i.indrelid=CAST(@relation AS oid)))
          OR (d.classid='pg_catalog.pg_class'::regclass AND EXISTS
            (SELECT 1 FROM pg_catalog.pg_class t WHERE t.oid=CAST(@relation AS oid) AND t.reltoastrelid=d.objid))
          OR (d.classid='pg_catalog.pg_attrdef'::regclass AND EXISTS
            (SELECT 1 FROM pg_catalog.pg_attrdef a WHERE a.oid=d.objid AND a.adrelid=CAST(@relation AS oid)))
          THEN 'dependent-local:'||d.deptype::text ELSE 'dependent-external:'||d.deptype::text END,
          o.type||':'||o.identity,o.identity||':'||d.refobjsubid::text
          FROM pg_catalog.pg_depend d CROSS JOIN LATERAL
            pg_catalog.pg_identify_object(d.classid,d.objid,d.objsubid) o
          WHERE d.refclassid='pg_catalog.pg_class'::regclass AND d.refobjid=CAST(@relation AS oid)
        """;

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
