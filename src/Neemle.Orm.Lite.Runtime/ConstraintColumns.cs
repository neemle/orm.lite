#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Neemle.Orm.Lite;

public sealed record ConstraintColumns(string Name, IReadOnlyList<string> Columns)
{
    private const string ConstraintSql =
        "SELECT a.attname FROM pg_constraint k CROSS JOIN LATERAL unnest(k.conkey) WITH ORDINALITY AS c(attnum, ord)"
        + " JOIN pg_attribute a ON a.attrelid = k.conrelid AND a.attnum = c.attnum WHERE k.conname = @n AND pg_table_is_visible(k.conrelid) ORDER BY c.ord";

    private const string IndexSql =
        "SELECT a.attname FROM pg_class r JOIN pg_index i ON i.indexrelid = r.oid CROSS JOIN LATERAL unnest(i.indkey::int2[]) WITH ORDINALITY AS c(attnum, ord)"
        + " JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = c.attnum"
        + " WHERE r.relname = @n AND r.relkind = 'i' AND pg_table_is_visible(r.oid) AND c.attnum > 0 ORDER BY c.ord";

    public static async Task<ConstraintColumns> ForAsync(OrmContext ctx, string? constraintOrIndexName)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var name = constraintOrIndexName ?? "";
        if (name.Length == 0) return new ConstraintColumns(name, []);

        var columns = await ReadAsync(ctx, ConstraintSql, name);
        if (columns.Count == 0) columns = await ReadAsync(ctx, IndexSql, name);
        return new ConstraintColumns(name, columns);
    }

    private static async Task<List<string>> ReadAsync(OrmContext ctx, string sql, string name)
    {
        var columns = new List<string>();
        await using var cmd = ctx.DataSource.CreateCommand(sql);
        cmd.AddParam("n", name);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) columns.Add(reader.GetFieldValue<string>(0));
        return columns;
    }
}
