using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public static class DdlCompiler
{
    public static IReadOnlyList<Statement> Compile(Node op, ISqlProvider provider)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(provider);
        Walk(op, node => Check(node, provider));
        var lowered = provider.Lower(op);
        if (lowered is Script script)
            return script.Statements.Select(s => StatementCompiler.Compile(s, provider, StatementKind.Ddl)).ToList();
        return [StatementCompiler.Compile(lowered, provider, StatementKind.Ddl)];
    }

    public static void Validate(Node op, ISqlProvider provider)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(provider);
        Walk(op, node => Check(node, provider));
    }

    public static IReadOnlyList<string> Render(IEnumerable<Node> ops, ISqlProvider provider) =>
        ops.SelectMany(op => Compile(op, provider)).Select(s => s.Text).ToList();

    private static void Check(Node node, ISqlProvider provider)
    {
        DeclaredIdentifiers.Check(node, provider);
        if (node is RawSql { ProviderId: { } scope } && !string.Equals(scope, provider.Id, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"raw SQL is scoped to provider '{scope}' and cannot run on provider '{provider.Id}'.");
        foreach (var capability in node.Requires())
            if (provider.Capabilities.Of(capability) == Support.None)
                throw (node is ReconcileIndex reconcile ? reconcile.Create : node as CreateIndex) is { } index
                    ? new CapabilityNotSupportedException(provider.Id, node.Name, capability, index.Table, "Index '" + index.Index + "'.")
                    : new CapabilityNotSupportedException(provider.Id, node.Name, capability);
    }

    private static void Walk(Node node, Action<Node> visit)
    {
        visit(node);
        foreach (var child in node.Children()) Walk(child, visit);
    }
}
