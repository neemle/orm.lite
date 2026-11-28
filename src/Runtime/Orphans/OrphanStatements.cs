using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class OrphanStatements
{
    internal static IReadOnlyList<OrphanCommand> Build<TKey>(OrphanOperation operation, OrphanPrepared<TKey> plan)
        where TKey : notnull
    {
        var commands = new List<OrphanCommand>();
        foreach (var write in plan.Present)
        {
            var insert = write.Intent == OrphanPresentIntent.Insert;
            var tree = insert ? EntityStatements.Insert(operation.Child, write.Values)
                : PresentUpdate(operation, write.Values);
            commands.Add(Compile(operation, tree, insert ? OrphanError.ProviderFailure : OrphanError.MissingExisting));
        }
        foreach (var key in plan.Candidates)
            commands.Add(Compile(operation, Candidate(operation, key), OrphanError.StaleAuthority));
        return commands.AsReadOnly();
    }

    private static Node PresentUpdate(OrphanOperation operation, IReadOnlyList<object?> values)
    {
        var update = (Update)EntityStatements.Update(operation.Child, values);
        var owner = Owner(operation, operation.Child.Columns.Count);
        return update with { Where = new Where(NodeList.Of(update.Where!.Terms.Append(owner))) };
    }

    private static Node Candidate(OrphanOperation operation, object key)
    {
        var child = operation.Child;
        var terms = new List<Node>
        {
            new Compare(CompareOp.Equal, new ColumnRef(child.SoleKey!.Column), new Param(0, key, "key")),
            Owner(operation, 1),
        };
        if (child.Soft is { } soft) terms.Add(new IsNull(new ColumnRef(soft.Column)));
        var where = new Where(NodeList.Of(terms));
        if (operation.Options.Mode == OrphanMode.Delete) return new Delete(new TableRef(child.Table), where);
        var column = operation.Options.Mode == OrphanMode.Nullify
            ? operation.Relation.Owner.Column : child.Soft!.Column;
        Node value = operation.Options.Mode == OrphanMode.Nullify ? new NullLit() : new ClockNow();
        return new Update(new TableRef(child.Table), [new Assignment(new ColumnRef(column), value)], where);
    }

    private static Node Owner(OrphanOperation operation, int ordinal) => new Compare(CompareOp.Equal,
        new ColumnRef(operation.Relation.Owner.Column), new Param(ordinal, operation.ParentKey, "orphan_owner"));

    private static OrphanCommand Compile(OrphanOperation operation, Node tree, OrphanError missing)
    {
        var statement = StatementCompiler.Compile(tree, operation.Provider, StatementKind.Write, operation.Child.Table);
        if (statement.Parameters.Count > operation.Provider.MaxBindParameters)
            throw OrphanRefusal.At(OrphanError.LimitExceeded);
        return new(statement, missing);
    }
}
