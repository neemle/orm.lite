namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaRoles
{
    private const string Sql = """
        SELECT 'constraint:'||c.contype::text,c.conname::text,c.contype::text,a.attname::text
          FROM pg_catalog.pg_constraint c JOIN pg_catalog.pg_attribute a
            ON a.attrelid=c.conrelid AND a.attnum=c.conkey[1]
          WHERE c.conrelid=CAST(@relation AS oid) AND c.contype IN ('p','n')
            AND cardinality(c.conkey)=1 AND NOT c.condeferrable AND NOT c.condeferred
            AND c.convalidated AND c.conenforced AND c.conparentid=0
        UNION ALL SELECT 'index',i.relname::text,'index',a.attname::text
          FROM pg_catalog.pg_index x JOIN pg_catalog.pg_class i ON i.oid=x.indexrelid
          JOIN pg_catalog.pg_am am ON am.oid=i.relam
          JOIN pg_catalog.pg_attribute a ON a.attrelid=x.indrelid AND a.attnum=x.indkey[0]
          JOIN pg_catalog.pg_constraint c ON c.conindid=i.oid AND c.contype='p'
          WHERE x.indrelid=CAST(@relation AS oid) AND x.indnatts=1 AND x.indnkeyatts=1
            AND x.indisprimary AND x.indisunique AND x.indisvalid AND x.indisready
            AND x.indpred IS NULL AND x.indexprs IS NULL AND i.reloptions IS NULL
            AND am.amname='btree' AND x.indoption[0]=0 AND x.indcollation[0]=a.attcollation
        UNION ALL SELECT 'dependent-local:'||d.deptype::text,o.type||':'||o.identity,
          CASE WHEN t.typrelid=CAST(@relation AS oid) THEN 'row-type'
            WHEN own.reltoastrelid=d.objid AND d.classid='pg_catalog.pg_class'::regclass
              THEN CASE WHEN toast.reloptions IS NULL AND toast.reltablespace=0
                THEN 'toast' ELSE 'toast-unsupported' END
            WHEN ad.adrelid=CAST(@relation AS oid) THEN 'default'
            WHEN c.conrelid=CAST(@relation AS oid) THEN 'constraint' ELSE '' END,
          COALESCE(c.conname::text,a.attname::text,'')
          FROM pg_catalog.pg_depend d CROSS JOIN LATERAL
            pg_catalog.pg_identify_object(d.classid,d.objid,d.objsubid) o
          LEFT JOIN pg_catalog.pg_type t ON d.classid='pg_catalog.pg_type'::regclass AND t.oid=d.objid
          LEFT JOIN pg_catalog.pg_class own ON own.oid=CAST(@relation AS oid)
          LEFT JOIN pg_catalog.pg_class toast ON toast.oid=own.reltoastrelid
          LEFT JOIN pg_catalog.pg_attrdef ad ON d.classid='pg_catalog.pg_attrdef'::regclass AND ad.oid=d.objid
          LEFT JOIN pg_catalog.pg_attribute a ON a.attrelid=ad.adrelid AND a.attnum=ad.adnum
          LEFT JOIN pg_catalog.pg_constraint c ON d.classid='pg_catalog.pg_constraint'::regclass AND c.oid=d.objid
          WHERE d.refclassid='pg_catalog.pg_class'::regclass AND d.refobjid=CAST(@relation AS oid)
        """;

    internal static async Task<IReadOnlyList<object?[]>> ReadAsync(PostgresSchemaRead read,
        uint relation, CancellationToken ct) => await read.RowsAsync(Sql,
            PostgresSchemaRead.Parameter("relation", relation), read.Request.Target.Limits.MaxCatalogObjects, ct);

    internal static IReadOnlyList<object?[]> Normalize(IReadOnlyList<object?[]> rows,
        IReadOnlyList<object?[]> roles, string table)
    {
        var constraints = new Dictionary<string, object?[]>(StringComparer.Ordinal);
        foreach (var role in roles.Where(r => (string)r[2]! is "p" or "n"))
        {
            var kind = (string)role[2]!; var column = (string)role[3]!;
            if ((string)role[1]! != PostgresSerializer.StrictName(table, column, kind == "p" ? "pk" : "nn")) continue;
            constraints[(string)role[1]!] = new object?[] { role[0], role[1],
                Framed(kind == "p" ? "primary-key-v1" : "not-null-v1", column), true };
        }
        return rows.Select(row => Normalize(row, roles, constraints, table)).ToArray();
    }

    private static object?[] Normalize(object?[] row, IReadOnlyList<object?[]> roles,
        IReadOnlyDictionary<string, object?[]> constraints, string table)
    {
        var kind = (string)row[0]!; var name = (string)row[1]!;
        if (kind.StartsWith("constraint:", StringComparison.Ordinal) && constraints.TryGetValue(name, out var stamp))
            return stamp;
        var role = roles.SingleOrDefault(r => (string)r[0]! == kind && (string)r[1]! == name);
        if (role is null) return row;
        var value = (string)role[3]!;
        return (string)role[2]! switch
        {
            "row-type" => Intrinsic("row-type", "row-type", Framed("row-type-v1")),
            "toast" => Intrinsic("toast", "toast-storage", Framed("toast-v1")),
            "default" => Intrinsic("default", value, Framed("default-v1", value)),
            "constraint" when constraints.TryGetValue(value, out var constraint) =>
                new object?[] { "dependent-local:constraint", value, constraint[2], true },
            "index" when name == PostgresSerializer.StrictName(table, value, "pk") =>
                new object?[] { "index:pk", name, Framed("primary-index-v1", value), true },
            _ => row,
        };
    }

    private static object?[] Intrinsic(string kind, string name, string definition) =>
        new object?[] { "dependent-local:" + kind, name, definition };

    private static string Framed(params string[] values) => string.Concat(values.Select(value =>
        value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + value));
}
