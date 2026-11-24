#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public sealed record ConstraintColumns(string Name, IReadOnlyList<string> Columns)
{
    public static async Task<ConstraintColumns> ForAsync(OrmContext ctx, string? constraintOrIndexName)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var name = constraintOrIndexName ?? "";
        if (name.Length == 0) return new ConstraintColumns(name, []);

        var columns = await ReadAsync(ctx, ConstraintSource.Constraint, name);
        if (columns.Count == 0) columns = await ReadAsync(ctx, ConstraintSource.Index, name);
        return new ConstraintColumns(name, columns);
    }

    private static async Task<List<string>> ReadAsync(OrmContext ctx, ConstraintSource source, string name)
    {
        var columns = new List<string>();
        var statement = HistoryStatements.Compile(ctx.Provider, HistoryStatements.ConstraintColumns(source, name));
        await using var cmd = ctx.DataSource.CreateCommand(statement.Text);
        HistoryStatements.Bind(cmd, statement);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) columns.Add(reader.GetFieldValue<string>(0));
        return columns;
    }
}
