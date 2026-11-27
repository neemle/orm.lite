namespace Neemle.OrmLite;

internal static class OrphanPayloadCapture
{
    internal static OrphanFrozenWrite<TKey> Copy<TChild, TKey>(OrphanOperation operation,
        OrphanPresentWrite<TChild> supplied) where TChild : class where TKey : notnull
    {
        if (!Enum.IsDefined(supplied.Intent)) throw OrphanRefusal.At(OrphanError.InvalidRequest);
        if (supplied.Child.GetType() != operation.Relation.Child.EntityType)
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        var values = operation.Relation.Child.Values(supplied.Child);
        var columns = operation.Child.Columns;
        var keyIndex = columns.ToList().FindIndex(c => c.Key);
        var ownerIndex = columns.ToList().FindIndex(c => c.Column == operation.Relation.Owner.Column);
        if (values[keyIndex] is not TKey key || values.Any(v => !OrphanScalarTypes.Immutable(v)))
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        if (!operation.Relation.Parent.KeyComparer.Equals(values[ownerIndex], operation.ParentKey))
            throw OrphanRefusal.At(OrphanError.ParentMismatch);
        return new(key, supplied.Intent, Array.AsReadOnly(values));
    }
}
