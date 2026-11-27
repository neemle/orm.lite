namespace Neemle.OrmLite;

public sealed class JoinColumn<TValue>
{
    internal JoinColumn(JoinColumnReference column) => Reference = column;
    internal JoinColumnReference Reference { get; }

    public JoinPredicate Equal(JoinColumn<TValue> other) => Compare(other, JoinPredicateKind.Equal);
    public JoinPredicate NotEqual(JoinColumn<TValue> other) => Compare(other, JoinPredicateKind.NotEqual);
    public JoinPredicate LessThan(JoinColumn<TValue> other) => Compare(other, JoinPredicateKind.Less);
    public JoinPredicate GreaterThan(JoinColumn<TValue> other) => Compare(other, JoinPredicateKind.Greater);
    public JoinPredicate EqualValue(TValue value) => Constant(value, JoinPredicateKind.Equal);
    public JoinPredicate NotEqualValue(TValue value) => Constant(value, JoinPredicateKind.NotEqual);
    public JoinPredicate LessThanValue(TValue value) => Constant(value, JoinPredicateKind.Less);
    public JoinPredicate GreaterThanValue(TValue value) => Constant(value, JoinPredicateKind.Greater);
    public JoinPredicate IsNull() => JoinPredicate.Null(Reference, false);
    public JoinPredicate IsNotNull() => JoinPredicate.Null(Reference, true);

    private JoinPredicate Compare(JoinColumn<TValue> other, JoinPredicateKind kind) => other is null
        ? throw new JoinReadException(JoinError.InvalidRequest)
        : JoinPredicate.Columns(Reference, other.Reference, kind);
    private JoinPredicate Constant(TValue value, JoinPredicateKind kind) =>
        JoinPredicate.Constant(Reference, value, kind);
}
