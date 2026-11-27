namespace Neemle.OrmLite;

internal sealed class JoinEntityRegistration(JoinEntityMetadata metadata)
{
    private readonly Dictionary<string, IJoinColumnBinding> _columns = new(StringComparer.Ordinal);
    internal JoinEntityMetadata Metadata { get; } = metadata;

    internal void Add(IJoinColumnBinding column)
    {
        Metadata.Current();
        if (_columns.TryGetValue(column.Property, out var old))
        {
            if (old.ValueType == column.ValueType && old.Reader.Equals(column.Reader)) return;
            throw new JoinReadException(JoinError.UnsupportedSource);
        }
        _columns.Add(column.Property, column);
    }

    internal IReadOnlyDictionary<string, IJoinColumnBinding> Snapshot()
    {
        Metadata.Current();
        if (!_columns.Values.Any(c => c.Column.Key)) throw new JoinReadException(JoinError.UnsupportedSource);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, IJoinColumnBinding>(
            new Dictionary<string, IJoinColumnBinding>(_columns, StringComparer.Ordinal));
    }
}
