using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class JoinExpressionCompiler(JoinCompilationContext context)
{
    internal Node Source(Node node, QueryJoinSnapshot source) => node switch
    {
        And n => new And(Source(n.Left, source), Source(n.Right, source)),
        Or n => new Or(Source(n.Left, source), Source(n.Right, source)),
        Not n => new Not(Source(n.Operand, source)),
        Group n => new Group(Source(n.Inner, source)),
        _ => SourceLeaf(node, source),
    };

    private Node SourceLeaf(Node node, QueryJoinSnapshot source) => node switch
    {
        Compare n => CompareSource(n, source),
        IsNull n => new IsNull(Operand(n.Operand, source)),
        IsNotNull n => new IsNotNull(Operand(n.Operand, source)),
        ColumnRef n => context.SourceColumn(n, source),
        BoolLit n => n,
        _ => throw new JoinReadException(JoinError.UnsupportedExpression),
    };

    private Node CompareSource(Compare node, QueryJoinSnapshot source)
    {
        if (Null(node.Left)) return NullComparison(node.Op, Operand(node.Right, source));
        if (Null(node.Right)) return NullComparison(node.Op, Operand(node.Left, source));
        return new Compare(node.Op, Operand(node.Left, source), Operand(node.Right, source));
    }

    private static bool Null(Node node) => node is NullLit or Param { Value: null };
    private static Node NullComparison(CompareOp operation, Node operand) => operation switch
    {
        CompareOp.Equal => new IsNull(operand),
        CompareOp.NotEqual => new IsNotNull(operand),
        _ => throw new JoinReadException(JoinError.UnsupportedType),
    };

    private Node Operand(Node node, QueryJoinSnapshot source) => node switch
    {
        ColumnRef n => context.SourceColumn(n, source),
        Param n => context.Parameters.Bind(n.Value),
        IntLit n => context.Parameters.Bind(n.Value),
        StringLit n => context.Parameters.Bind(n.Value),
        BoolLit n => context.Parameters.Bind(n.Value),
        _ => throw new JoinReadException(JoinError.UnsupportedExpression),
    };

    internal Node Predicate(JoinPredicate node) => node.Kind switch
    {
        JoinPredicateKind.And => new And(Predicate(node.Left!), Predicate(node.Right!)),
        JoinPredicateKind.Or => new Or(Predicate(node.Left!), Predicate(node.Right!)),
        JoinPredicateKind.Not => new Not(Predicate(node.Left!)),
        _ => TypedLeaf(node),
    };

    private Node TypedLeaf(JoinPredicate node)
    {
        var left = context.Column(node.Column!, false);
        if (node.Kind == JoinPredicateKind.IsNull) return new IsNull(left);
        if (node.Kind == JoinPredicateKind.IsNotNull) return new IsNotNull(left);
        var operation = Operation(node.Kind);
        var right = node.Other is { } other ? context.Column(other, false) : context.Parameters.Bind(node.Value);
        return new Compare(operation, left, right);
    }

    private static CompareOp Operation(JoinPredicateKind kind) => kind switch
    {
        JoinPredicateKind.Equal => CompareOp.Equal,
        JoinPredicateKind.NotEqual => CompareOp.NotEqual,
        JoinPredicateKind.Less => CompareOp.Less,
        JoinPredicateKind.Greater => CompareOp.Greater,
        _ => throw new JoinReadException(JoinError.UnsupportedExpression),
    };
}
