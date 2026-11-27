namespace Neemle.OrmLite;

internal sealed record JoinColumnReference(QueryJoinSnapshot Source, IJoinColumnBinding Binding)
{
    internal void Predicate(bool ordered)
    {
        Source.Current();
        if (Binding.Calculated) throw new JoinReadException(JoinError.UnsupportedExpression);
        if (ordered) JoinScalarTypes.Ordered(Binding.ValueType);
    }
}
