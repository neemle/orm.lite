using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class JoinRegisteredColumn<TValue>(string property, ColumnDescriptor column,
    bool calculated, Func<DbDataReader, int, TValue> read) : IJoinColumnBinding
{
    public string Property { get; } = property;
    public ColumnDescriptor Column { get; } = column;
    public bool Calculated { get; } = calculated;
    public Type ValueType => typeof(TValue);
    public Delegate Reader => Read;
    internal Func<DbDataReader, int, TValue> Read { get; } = read;
}
