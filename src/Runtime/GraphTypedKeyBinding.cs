namespace Neemle.OrmLite;

public sealed class GraphKeyBinding<T, TKey> : GraphKeyBinding<T> where T : class where TKey : notnull
{
    private readonly Func<T, TKey> _read;

    public GraphKeyBinding(string column, Func<T, TKey> read)
    {
        Column = column ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
        _read = read ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
    }

    internal override bool Equivalent(GraphKeyBinding<T> other) => other is GraphKeyBinding<T, TKey> typed
        && Column == typed.Column && _read.Equals(typed._read);

    internal override string Column { get; }
    internal override Type KeyType => typeof(TKey);
    internal override IEqualityComparer<object> Comparer { get; } = new GraphKeyComparer<TKey>();
    internal override object Read(T value) => _read(value)
        ?? throw new GraphRequestException(GraphRequestError.InvalidBinding);
    internal override object Read(System.Data.Common.DbDataReader reader, int ordinal) =>
        reader.GetFieldValue<TKey>(ordinal);
}
