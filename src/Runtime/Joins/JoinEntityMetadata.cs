namespace Neemle.OrmLite;

internal sealed class JoinEntityMetadata
{
    private readonly EntityDescriptor _descriptor;
    private readonly ColumnDescriptor[] _columns;
    private readonly (string Property, string Alias, string Source)[] _calculated;
    private readonly string? _soft;

    internal JoinEntityMetadata(Type type, EntityDescriptor descriptor)
    {
        Type = type;
        _descriptor = descriptor;
        _columns = descriptor.Columns.ToArray();
        _calculated = descriptor.Calculated.Select(c => (c.Property, c.Alias, c.SourceColumn)).ToArray();
        _soft = descriptor.SoftColumn;
        Binding = GraphModel.Find(type);
        _ = JoinScalarTypes.Kind(Binding.KeyType);
        if (descriptor.IsView || descriptor.SoleKey is not { Nullable: false })
            throw new JoinReadException(JoinError.UnsupportedSource);
        if (!ReferenceEquals(Binding.Descriptor, descriptor)) throw new JoinReadException(JoinError.UnsupportedSource);
        Current();
    }

    internal Type Type { get; }
    internal IGraphEntityBinding Binding { get; }
    internal string Table => _descriptor.Table;
    internal IReadOnlyList<ColumnDescriptor> Columns => Array.AsReadOnly(_columns);
    internal string KeyColumn => Binding.KeyColumn;

    internal void Descriptor(EntityDescriptor descriptor)
    {
        Current();
        if (!ReferenceEquals(_descriptor, descriptor)) throw new JoinReadException(JoinError.UnsupportedSource);
    }

    internal void Current()
    {
        if (!ReferenceEquals(OrmMeta.ReadDescriptor(Type), _descriptor))
            throw new JoinReadException(JoinError.UnsupportedSource);
        if (_descriptor.IsView || !_columns.SequenceEqual(_descriptor.Columns))
            throw new JoinReadException(JoinError.UnsupportedSource);
        var current = _descriptor.Calculated.Select(c => (c.Property, c.Alias, c.SourceColumn));
        if (!_calculated.SequenceEqual(current) || _soft != _descriptor.SoftColumn)
            throw new JoinReadException(JoinError.UnsupportedSource);
    }

    internal (ColumnDescriptor Column, bool Calculated) Property(string property)
    {
        if (_columns.SingleOrDefault(c => c.Property == property) is { } column) return (column, false);
        var calculated = _calculated.SingleOrDefault(c => c.Property == property);
        var source = _columns.SingleOrDefault(c => c.Column == calculated.Source);
        if (source is null || source.Type != Nodes.ColumnType.Text)
            throw new JoinReadException(JoinError.UnsupportedExpression);
        return (new ColumnDescriptor(property, calculated.Alias, source.Type, source.Nullable, false), true);
    }
}
