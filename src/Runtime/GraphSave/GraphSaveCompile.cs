using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class GraphSaveCompile
{
    internal static IReadOnlyList<GraphSaveStatement> All(GraphSavePrepared prepared, ISqlProvider provider)
    {
        var statements = new List<GraphSaveStatement>();
        foreach (var anchor in prepared.Anchors)
            statements.Add(Compile(Anchor(anchor), provider, GraphSaveError.MissingAnchor, true));
        foreach (var node in prepared.Writes)
        {
            var tree = node.Intent == GraphSaveIntent.Insert
                ? EntityStatements.Insert(node.Binding.Descriptor, node.Values)
                : EntityStatements.Update(node.Binding.Descriptor, node.Values);
            statements.Add(Compile(tree, provider,
                node.Intent == GraphSaveIntent.Update ? GraphSaveError.MissingExisting : null, false));
        }
        foreach (var link in prepared.Links)
        {
            var statement = StatementCompiler.Compile(Link(link), provider, StatementKind.Write, link.Table);
            Capacity(statement, provider);
            statements.Add(new(statement, null, false, true));
        }
        return statements.AsReadOnly();
    }

    private static GraphSaveStatement Compile(Node tree, ISqlProvider provider, GraphSaveError? missing, bool anchor)
    {
        var statement = StatementCompiler.Compile(tree, provider, anchor ? StatementKind.Read : StatementKind.Write);
        Capacity(statement, provider);
        return new(statement, missing, anchor, false);
    }

    private static void Capacity(Statement statement, ISqlProvider provider)
    {
        if (statement.Parameters.Count > provider.MaxBindParameters)
            throw GraphSaveRefusal.At(GraphSaveError.LimitExceeded);
    }

    private static Select Anchor(GraphSaveNode node)
    {
        var shape = (Select)EntityStatements.Get(node.Binding.Descriptor, node.Key);
        return shape with { Columns = [new ColumnRef(node.Binding.Descriptor.SoleKey!.Column)], Limit = new Limit(1) };
    }

    private static Insert Link(GraphSaveLink link) => new(new TableRef(link.Table),
        [new ColumnRef(link.FirstColumn), new ColumnRef(link.SecondColumn)],
        [new ValuesRow([new Param(0, link.FirstKey), new Param(1, link.SecondKey)])],
        new Conflict([new ColumnRef(link.FirstColumn), new ColumnRef(link.SecondColumn)]));
}
