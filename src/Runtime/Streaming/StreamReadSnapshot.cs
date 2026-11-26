using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class StreamReadSnapshot<T>
{
    private readonly Statement _statement;

    internal StreamReadSnapshot(Statement statement, Func<DbDataReader, T> map,
        StreamBinding binding, StreamBorrowed? borrowed)
    {
        _statement = StreamParameterValues.Copy(statement);
        Map = map;
        Binding = binding;
        Borrowed = borrowed;
    }

    internal StreamBinding Binding { get; }
    internal StreamBorrowed? Borrowed { get; }
    internal Func<DbDataReader, T> Map { get; }
    internal Statement FreshStatement() => StreamParameterValues.Copy(_statement);
}
