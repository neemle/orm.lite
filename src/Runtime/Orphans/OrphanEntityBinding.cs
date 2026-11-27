using System.Data.Common;

namespace Neemle.OrmLite;

public sealed class OrphanEntityBinding<T, TKey> : IOrphanEntity where T : class where TKey : notnull
{
    private readonly Func<T, object?[]> _values;
    private readonly Func<T, TKey> _key;
    private readonly OrphanMetadata _metadata;

    public OrphanEntityBinding(EntityDescriptor descriptor, Func<T, object?[]> physicalValues, Func<T, TKey> key)
    {
        if (physicalValues is null || key is null) throw OrphanRefusal.At(OrphanError.InvalidRequest);
        _metadata = new(descriptor);
        if (!OrphanScalarTypes.Supports<TKey>(_metadata.Key.Type))
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        _values = physicalValues;
        _key = key;
    }

    Type IOrphanEntity.EntityType => typeof(T);
    Type IOrphanEntity.KeyType => typeof(TKey);
    OrphanMetadata IOrphanEntity.Metadata => _metadata;
    IEqualityComparer<object> IOrphanEntity.KeyComparer { get; } = new OrphanKeyComparer<TKey>();
    object IOrphanEntity.Key(object entity) => _key(Admit(entity))
        ?? throw OrphanRefusal.At(OrphanError.UnsupportedMapping);

    object?[] IOrphanEntity.Values(object entity)
    {
        var typed = Admit(entity);
        var supplied = _values(typed);
        if (supplied is null) throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        var copy = (object?[])supplied.Clone();
        ValidateValues(copy);
        var keyIndex = _metadata.Columns.ToList().FindIndex(c => c.Key);
        if (copy[keyIndex] is not TKey key || !EqualityComparer<TKey>.Default.Equals(key, _key(typed)))
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        ((IOrphanEntity)this).ValidateCurrent();
        return copy;
    }

    object IOrphanEntity.ReadKey(DbDataReader reader, int ordinal)
    {
        try
        {
            if (reader.IsDBNull(ordinal)) throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
            var key = ScalarValue.To<TKey>(reader.GetValue(ordinal), "", _metadata.Key.Column);
            return key ?? throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        }
        catch (Exception cause) when (cause is not OrphanException)
        { throw new OrphanException(OrphanError.UnsupportedMapping, OrphanOutcome.NotStarted, cause); }
    }

    bool IOrphanEntity.Equivalent(IOrphanEntity other) => other is OrphanEntityBinding<T, TKey> typed
        && _metadata.Equivalent(typed._metadata) && _values.Equals(typed._values) && _key.Equals(typed._key);

    void IOrphanEntity.ValidateCurrent()
    {
        if (PolicyReadHooks<T>.Rows is not null || PolicyReadHooks<T>.First is not null
            || PolicyReadHooks<T>.Snapshot is not null)
            throw OrphanRefusal.At(OrphanError.UnsupportedVisibility);
        _metadata.ValidateCurrent(typeof(T));
    }

    private T Admit(object entity)
    {
        if (entity is not T typed) throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        ((IOrphanEntity)this).ValidateCurrent();
        OrphanReadOrigins.RefuseKnownPartial(typed);
        return typed;
    }

    private void ValidateValues(object?[] values)
    {
        if (values.Length != _metadata.Columns.Count) throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        for (var i = 0; i < values.Length; i++)
            if (!OrphanScalarTypes.Value(_metadata.Columns[i], values[i]))
                throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
    }
}
