using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class GraphLinkReader
{
    internal static async Task<List<(TSourceKey Source, TTargetKey Target)>> ReadAsync<TSource, TSourceKey, TTargetKey>(
        GraphReadContext context, GraphPlanNode node, IReadOnlyList<object> keys)
        where TSource : class where TSourceKey : notnull where TTargetKey : notnull
    {
        var source = GraphModel.Find(typeof(TSource));
        var link = node.Edge.Link!;
        var shape = new Select([new ColumnRef(link.SourceColumn), new ColumnRef(link.TargetColumn)],
            new TableRef(link.Table));
        var result = new List<(TSourceKey Source, TTargetKey Target)>();
        foreach (var chunk in keys.Chunk(context.Database.Provider.MaxBindParameters))
        {
            var filtered = GraphFrontier.Filter(shape, link.SourceColumn, chunk, context.Budget.RelatedRemaining);
            var statement = StatementCompiler.Compile(filtered, context.Database.Provider,
                StatementKind.Read, link.Table);
            var rows = await GraphStatementRunner.ReadAsync(context, statement,
                reader => Keys<TSourceKey, TTargetKey>(reader, source, node.Target.Binding));
            result.AddRange(rows.Where(row => row.HasValue).Select(row => row.GetValueOrDefault()));
        }
        return result;
    }

    private static (TSourceKey Source, TTargetKey Target)? Keys<TSourceKey, TTargetKey>(
        DbDataReader reader, IGraphEntityBinding source, IGraphEntityBinding target)
        where TSourceKey : notnull where TTargetKey : notnull
    {
        if (reader.IsDBNull(0) || reader.IsDBNull(1)) return null;
        return (Checked<TSourceKey>(source.ReadKey(reader, 0)), Checked<TTargetKey>(target.ReadKey(reader, 1)));
    }

    private static TKey Checked<TKey>(object value) where TKey : notnull => value is TKey key ? key
        : throw new GraphRequestException(GraphRequestError.InvalidBinding);
}
