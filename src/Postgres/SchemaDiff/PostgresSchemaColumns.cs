using System.Globalization;

namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaColumns
{
    private const string Sql = """
        SELECT a.attname, pg_catalog.format_type(a.atttypid,a.atttypmod), NOT a.attnotnull,
          COALESCE((SELECT k.ordinality::integer FROM pg_catalog.pg_constraint c
            CROSS JOIN LATERAL unnest(c.conkey) WITH ORDINALITY k(attnum,ordinality)
            WHERE c.conrelid=a.attrelid AND c.contype='p' AND k.attnum=a.attnum),0),
          pg_catalog.pg_get_expr(d.adbin,d.adrelid), a.attgenerated<>'', a.attidentity<>'',
          CASE WHEN a.attcollation=t.typcollation THEN NULL ELSE cn.nspname||'.'||co.collname END
        FROM pg_catalog.pg_attribute a JOIN pg_catalog.pg_type t ON t.oid=a.atttypid
        LEFT JOIN pg_catalog.pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum
        LEFT JOIN pg_catalog.pg_collation co ON co.oid=a.attcollation
        LEFT JOIN pg_catalog.pg_namespace cn ON cn.oid=co.collnamespace
        WHERE a.attrelid=CAST(@relation AS oid) AND a.attnum>0 AND NOT a.attisdropped ORDER BY a.attnum
        """;

    internal static async Task<IReadOnlyList<SchemaObservedColumn>> ReadAsync(
        PostgresSchemaRead read, uint relation, CancellationToken ct)
    {
        var rows = await read.RowsAsync(Sql, PostgresSchemaRead.Parameter("relation", relation),
            read.Request.Target.Limits.MaxColumns, ct);
        return rows.Select(Column).ToArray();
    }

    private static SchemaObservedColumn Column(object?[] row) => new((string)row[0]!,
        PostgresSchemaTypes.Parse((string)row[1]!), SchemaFact<bool>.Known((bool)row[2]!),
        SchemaFact<int>.Known(Convert.ToInt32(row[3], CultureInfo.InvariantCulture)),
        new SchemaObservedColumnFacts(PostgresSchemaTypes.Default((string?)row[4]),
            SchemaFact<bool>.Known((bool)row[5]!), SchemaFact<bool>.Known((bool)row[6]!),
            SchemaFact<string?>.Known((string?)row[7])));
}
