namespace Neemle.OrmLite;

public sealed class JoinPredicate
{
    private JoinPredicate(JoinPredicateKind kind, JoinColumnReference column,
        JoinColumnReference? other = null, object? value = null)
    { Kind = kind; Column = column; Other = other; Value = value; }

    private JoinPredicate(JoinPredicateKind kind, JoinPredicate left, JoinPredicate? right = null)
    { Kind = kind; Left = left; Right = right; }

    internal JoinPredicateKind Kind { get; }
    internal JoinColumnReference? Column { get; }
    internal JoinColumnReference? Other { get; }
    internal object? Value { get; }
    internal JoinPredicate? Left { get; }
    internal JoinPredicate? Right { get; }

    internal static JoinPredicate Columns(JoinColumnReference column, JoinColumnReference other,
        JoinPredicateKind kind)
    {
        if (other is null) throw new JoinReadException(JoinError.InvalidRequest);
        var ordered = kind is JoinPredicateKind.Less or JoinPredicateKind.Greater;
        column.Predicate(ordered); other.Predicate(ordered);
        if (column.Binding.Column.Nullable != other.Binding.Column.Nullable)
            throw new JoinReadException(JoinError.UnsupportedType);
        return new(kind, column, other);
    }

    internal static JoinPredicate Constant(JoinColumnReference column, object? value, JoinPredicateKind kind)
    {
        var ordered = kind is JoinPredicateKind.Less or JoinPredicateKind.Greater;
        column.Predicate(ordered);
        value = JoinScalarTypes.Value(value);
        if (value is null)
        {
            if (ordered) throw new JoinReadException(JoinError.UnsupportedType);
            var test = kind == JoinPredicateKind.Equal ? JoinPredicateKind.IsNull : JoinPredicateKind.IsNotNull;
            return new(test, column);
        }
        return new(kind, column, value: value);
    }

    internal static JoinPredicate Null(JoinColumnReference column, bool negate)
    { column.Predicate(false); return new(negate ? JoinPredicateKind.IsNotNull : JoinPredicateKind.IsNull, column); }

    public JoinPredicate And(JoinPredicate other) => Combine(JoinPredicateKind.And, other);
    public JoinPredicate Or(JoinPredicate other) => Combine(JoinPredicateKind.Or, other);
    public JoinPredicate Not() => new(JoinPredicateKind.Not, this);

    private JoinPredicate Combine(JoinPredicateKind kind, JoinPredicate other) => other is null
        ? throw new JoinReadException(JoinError.InvalidRequest) : new(kind, this, other);
}
