using System.Data.Common;

namespace Neemle.OrmLite;

public sealed class GraphEntityBinding<T> : IGraphEntityBinding where T : class
{
    private readonly Func<DbDataReader, T> _map;
    private readonly GraphKeyBinding<T> _key;
    private readonly IReadOnlyList<IGraphNavigationBinding> _navigations;

    public GraphEntityBinding(EntityDescriptor descriptor, Func<DbDataReader, T> map,
        GraphKeyBinding<T> key, params GraphNavigationBinding<T>[] navigations)
    {
        Descriptor = descriptor ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
        _map = map ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
        _key = key ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
        if (navigations is null || navigations.Any(n => n is null))
            throw new GraphRequestException(GraphRequestError.InvalidBinding);
        _navigations = Array.AsReadOnly(navigations.Cast<IGraphNavigationBinding>().ToArray());
        if (descriptor.SoleKey?.Column != key.Column
            || _navigations.Select(n => n.Navigation).Distinct().Count() != navigations.Length)
            throw new GraphRequestException(GraphRequestError.InvalidBinding);
    }

    internal EntityDescriptor Descriptor { get; }
    Type IGraphEntityBinding.EntityType => typeof(T);
    EntityDescriptor IGraphEntityBinding.Descriptor => Descriptor;
    string IGraphEntityBinding.KeyColumn => _key.Column;
    Type IGraphEntityBinding.KeyType => _key.KeyType;
    IEqualityComparer<object> IGraphEntityBinding.Comparer => _key.Comparer;
    IReadOnlyList<IGraphNavigationBinding> IGraphEntityBinding.Navigations => _navigations;
    object IGraphEntityBinding.Key(object value) => value is T typed ? _key.Read(typed) : throw Invalid();
    object IGraphEntityBinding.ReadKey(DbDataReader reader, int ordinal) => _key.Read(reader, ordinal);
    object IGraphEntityBinding.Map(DbDataReader reader) => _map(reader);

    bool IGraphEntityBinding.Maps(Delegate map) => _map.Equals(map);

    bool IGraphEntityBinding.Equivalent(IGraphEntityBinding other) => other is GraphEntityBinding<T> typed
        && SameDescriptor(typed.Descriptor) && _map.Equals(typed._map) && _key.Equivalent(typed._key)
        && _key.KeyType == typed._key.KeyType && _navigations.Count == typed._navigations.Count
        && _navigations.Zip(typed._navigations).All(pair => pair.First.Equivalent(pair.Second));

    private bool SameDescriptor(EntityDescriptor other) => Descriptor.Table == other.Table
        && Descriptor.ClrType == other.ClrType && Descriptor.Managed == other.Managed
        && Descriptor.IsView == other.IsView && Descriptor.Columns.SequenceEqual(other.Columns)
        && Descriptor.ManyToMany.SequenceEqual(other.ManyToMany) && Descriptor.SoftColumn == other.SoftColumn
        && Descriptor.Calculated.Select(c => (c.Property, c.Alias, c.SourceColumn))
            .SequenceEqual(other.Calculated.Select(c => (c.Property, c.Alias, c.SourceColumn)));

    private static GraphRequestException Invalid() => new(GraphRequestError.InvalidBinding);
}
